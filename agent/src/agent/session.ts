import {
  query as sdkQuery,
  type Options,
  type Query,
  type SDKMessage,
  type SDKUserMessage,
} from '@anthropic-ai/claude-agent-sdk';
import type { SidecarToPlugin, UserMessage } from '../ipc/protocol.js';
import { toSdkUserMessage } from './content.js';
import type { ToolBridge } from '../tools/common.js';
import { MCP_SERVER_NAME, createCadServer } from '../tools/index.js';
import { InputQueue } from './inputQueue.js';
import { SKILLS_PLUGIN_NAME, enabledSkills, prepareSkillsPlugin } from './skills.js';
import { SYSTEM_PROMPT } from './systemPrompt.js';

export type QueryFn = typeof sdkQuery;

export interface SessionConfig {
  emit: (msg: SidecarToPlugin) => void;
  bridge: ToolBridge;
  version: string;
  workDir: string;
  skillsDir?: string;
  model?: string;
  fallbackModel?: string;
  /** Effort level (low | medium | high | xhigh | max); undefined = model default. */
  effort?: string;
  log: (msg: string) => void;
  /** Called when the user cancels: pending tool calls must be failed. */
  onCancel?: () => void;
  /** Injection point for tests. */
  queryFn?: QueryFn;
}

/** Built-in Claude Code tools we never want in a CAD session (spec §5). */
export const DISALLOWED_BUILTINS = [
  'Bash', 'BashOutput', 'KillShell', 'Write', 'Edit', 'MultiEdit', 'NotebookEdit',
  'WebSearch', 'WebFetch', 'Glob', 'Grep', 'Task', 'Agent', 'TodoWrite',
  'ExitPlanMode', 'EnterPlanMode', 'AskUserQuestion', 'SlashCommand', 'Workflow', 'Monitor',
];

const ERROR_TEXT: Record<string, string> = {
  authentication_failed: 'Chưa đăng nhập Claude. Mở terminal, chạy "claude" rồi đăng nhập tài khoản Pro/Max (hoặc gõ /login), hoặc đặt biến môi trường ANTHROPIC_API_KEY; xong bấm New. (claude login)',
  oauth_org_not_allowed: 'Tài khoản Claude này không được phép dùng Claude Code.',
  billing_error: 'Lỗi thanh toán / hết hạn mức tài khoản Claude.',
  rate_limit: 'Đã chạm giới hạn sử dụng của gói Claude. Đợi một lúc rồi thử lại.',
  overloaded: 'Máy chủ Claude đang quá tải, thử lại sau ít phút.',
  model_not_found: 'Model không khả dụng với tài khoản này. Đặt CLAUDECAD_MODEL sang model khác (ví dụ claude-sonnet-5-5).',
  max_output_tokens: 'Câu trả lời quá dài và bị cắt.',
};

/**
 * One continuous Agent SDK conversation per chat window (spec §5): streaming
 * input keeps multi-turn context; New chat closes it and starts a fresh one.
 */
export class AgentSession {
  private q?: Query;
  private input?: InputQueue<SDKUserMessage>;
  private generation = 0;
  private turnActive = false;
  private cancelRequested = false;
  private textBlockOpen = false;
  private sessionId?: string;
  private initInfo = '';
  private lastUser = '';
  private lastConfirm?: 'yes' | 'no';
  /** Message of the running turn, re-sent once if resuming an old session fails. */
  private pending?: UserMessage;
  private resuming = false;
  private gotInit = false;
  /** Results still expected from turns abandoned by the plugin (forced end after Stop). */
  private staleResults = 0;
  private reloadPending = false;
  private readonly defaultModel?: string;

  constructor(private readonly cfg: SessionConfig) {
    this.defaultModel = cfg.model;
  }

  get busy(): boolean {
    return this.turnActive;
  }

  get currentSessionId(): string | undefined {
    return this.sessionId;
  }

  /** Text of the user message of the current/last turn (used by the confirmation gate). */
  get lastUserText(): string {
    return this.lastUser;
  }

  /** True when the current turn was started by the "Đồng ý" button of a confirmation card. */
  get lastUserConfirmed(): boolean {
    return this.lastConfirm === 'yes';
  }

  get info(): string {
    return this.initInfo;
  }

  /** Options handed to query(); exported for tests and documentation. */
  buildOptions(resume?: string): Options {
    const server = createCadServer(this.cfg.bridge, this.cfg.version, (req) => this.cfg.emit(req));
    const plugin = prepareSkillsPlugin(this.cfg.skillsDir, this.cfg.workDir);
    if (plugin) this.cfg.log(`skills loaded: ${plugin.skills.join(', ') || '(none)'}`);

    const options: Options = {
      cwd: this.cfg.workDir,
      systemPrompt: SYSTEM_PROMPT,
      model: this.cfg.model,
      fallbackModel: this.cfg.fallbackModel,
      ...(this.cfg.effort ? { effort: this.cfg.effort as Options['effort'] } : {}),
      mcpServers: { [MCP_SERVER_NAME]: server },
      strictMcpConfig: true,
      // Base tool set: only Read (to read skill files) and Skill. Everything CAD comes from our MCP server.
      tools: ['Read', 'Skill'],
      disallowedTools: DISALLOWED_BUILTINS,
      // Auto-allow our tools and Read; deny anything else without prompting.
      allowedTools: [`mcp__${MCP_SERVER_NAME}`, `mcp__${MCP_SERVER_NAME}__*`, 'Read', 'Skill'],
      permissionMode: 'dontAsk',
      permissionPrompts: 'none',
      // Isolation: ignore ~/.claude settings, CLAUDE.md, project MCP configs.
      settingSources: [],
      plugins: plugin ? [{ type: 'local', path: plugin.pluginDir, skipMcpDiscovery: true }] : [],
      // Only the ClaudeCAD skills — not whatever else is installed for Claude Code on this machine.
      skills: plugin ? plugin.skills.map((s) => `${SKILLS_PLUGIN_NAME}:${s}`) : [],
      includePartialMessages: true,
      persistSession: true,
      stderr: (data) => this.cfg.log(`[claude] ${data.trimEnd()}`),
      env: { ...process.env, CLAUDE_AGENT_SDK_CLIENT_APP: `claudecad/${this.cfg.version}` },
    };
    if (resume) options.resume = resume;
    return options;
  }

  private ensureStarted(): void {
    if (this.q) return;
    const gen = ++this.generation;
    const input = new InputQueue<SDKUserMessage>();
    const queryFn = this.cfg.queryFn ?? sdkQuery;
    // After an unexpected end of the Claude process (or when switching back to a
    // drawing) we resume the same conversation.
    const resume = this.sessionId;
    this.resuming = !!resume;
    this.gotInit = false;
    const q = queryFn({ prompt: input, options: this.buildOptions(resume) });
    this.q = q;
    this.input = input;
    void this.consume(q, gen);
  }

  /** Sends one user turn (plain text form, kept for callers and tests). */
  send(text: string, drawing?: string): void {
    this.sendMessage({ type: 'user_message', text, ...(drawing ? { drawing } : {}) });
  }

  /** Sends one user turn with attachments / context from the palette. */
  sendMessage(msg: UserMessage): void {
    if (this.turnActive) {
      // The plugin only sends when it considers the previous turn over (e.g. it force-ended a
      // turn after Stop). Abandon that turn here too instead of rejecting the new message,
      // otherwise the palette would wait forever for a turn_end.
      this.cfg.log('new message while a turn is active: abandoning the previous turn');
      this.cfg.onCancel?.();
      this.staleResults++;
      // safety: if the abandoned turn never reports a result, stop waiting for it
      setTimeout(() => { if (this.staleResults > 0) this.staleResults--; }, 15_000).unref?.();
      void this.q?.interrupt().catch(() => {});
      this.turnActive = false;
      this.cancelRequested = false;
      this.textBlockOpen = false;
    }
    try {
      this.ensureStarted();
    } catch (err) {
      this.cfg.emit({ type: 'turn_end', error: `Không khởi tạo được phiên Claude: ${(err as Error).message}` });
      return;
    }
    this.turnActive = true;
    this.cancelRequested = false;
    this.lastUser = msg.text;
    this.lastConfirm = msg.confirm;
    this.pending = msg;
    this.input!.push(toSdkUserMessage(msg));
  }

  /** Model / effort from the palette's settings; applied live when a query is running. */
  async setOptions(opts: { model?: string; effort?: string }): Promise<void> {
    // "default" = back to the configured model / the model's own default effort
    const model = opts.model === 'default' ? this.defaultModel : opts.model;
    const resetEffort = opts.effort === 'default';
    if (opts.model) this.cfg.model = model;
    if (opts.effort) this.cfg.effort = resetEffort ? undefined : opts.effort;
    const q = this.q;
    if (q) {
      try {
        if (opts.model) await q.setModel(model);
        if (opts.effort) await q.applyFlagSettings({ effortLevel: (resetEffort ? null : opts.effort) as never });
      } catch (err) {
        this.cfg.log(`set_options failed live, will apply on next session: ${(err as Error).message}`);
        if (!this.turnActive) this.closeQuery(); // next message starts a query with the new options (resumed)
      }
    }
    this.cfg.emit({ type: 'options', ...(this.cfg.model ? { model: this.cfg.model } : {}), ...(this.cfg.effort ? { effort: this.cfg.effort } : {}) });
  }

  /**
   * Skills changed in the palette. The skill list is fixed when a Claude process starts, so
   * the next message starts a new process that resumes the SAME conversation (context kept)
   * with the new skills. During a turn this waits until the turn ends.
   */
  reloadSkills(): void {
    if (this.turnActive) this.reloadPending = true;
    else this.closeQuery(); // sessionId is kept → resumed on the next message
    this.cfg.emit({ type: 'skills_reloaded', skills: enabledSkills(this.cfg.skillsDir) });
  }

  /** Per-drawing conversations: continue the given session (or a fresh one) on the next message. */
  switchTo(sessionId: string | undefined): void {
    if (this.turnActive) this.cfg.onCancel?.();
    this.closeQuery();
    this.turnActive = false;
    this.sessionId = sessionId;
  }

  /** Stop button: interrupts the running turn cleanly (spec §5). */
  async cancel(): Promise<void> {
    if (!this.turnActive || !this.q) return;
    this.cancelRequested = true;
    this.cfg.onCancel?.();
    const gen = this.generation;
    try {
      await this.q.interrupt();
    } catch (err) {
      this.cfg.log(`interrupt failed: ${(err as Error).message}`);
    }
    // The SDK normally ends the turn with a result message; make sure the UI is released.
    setTimeout(() => {
      if (gen === this.generation && this.turnActive && this.cancelRequested) this.finishTurn({ cancelled: true });
    }, 3000).unref?.();
  }

  /** New chat: drop the conversation and start a fresh one on the next message. */
  reset(): void {
    if (this.turnActive) this.cfg.onCancel?.();
    this.closeQuery();
    this.sessionId = undefined;
    this.turnActive = false;
    this.cfg.emit({ type: 'session_reset' });
  }

  close(): void {
    this.closeQuery();
  }

  private closeQuery(): void {
    this.generation++;
    const q = this.q;
    this.q = undefined;
    this.input?.close();
    this.input = undefined;
    try {
      q?.close();
    } catch {
      /* ignore */
    }
  }

  private async consume(q: Query, gen: number): Promise<void> {
    try {
      for await (const msg of q) {
        if (gen !== this.generation) break;
        this.onMessage(msg);
      }
    } catch (err) {
      if (gen === this.generation) {
        this.cfg.log(`query failed: ${(err as Error).stack ?? err}`);
        if (this.resuming && !this.gotInit && this.pending && this.turnActive) {
          // The saved conversation could not be resumed (deleted / incompatible): start fresh and re-send.
          const msg = this.pending;
          this.cfg.emit({ type: 'agent_error', message: 'Không khôi phục được cuộc chat cũ của bản vẽ này; tớ bắt đầu cuộc chat mới.' });
          this.q = undefined;
          this.input?.close();
          this.input = undefined;
          this.sessionId = undefined;
          this.turnActive = false;
          this.sendMessage(msg);
          return;
        }
        this.cfg.emit({ type: 'agent_error', message: `Phiên Claude bị lỗi: ${(err as Error).message}` });
      }
    } finally {
      if (gen === this.generation) {
        // The Claude process ended. Next send() starts a new one (resuming the conversation).
        this.q = undefined;
        this.input?.close();
        this.input = undefined;
        if (this.turnActive) this.finishTurn({ error: 'Tiến trình Claude kết thúc bất thường. Gửi lại yêu cầu để tiếp tục.' });
      }
    }
  }

  private finishTurn(end: { error?: string; cancelled?: boolean; usage?: { input_tokens: number; output_tokens: number; session_cost_usd?: number } }): void {
    if (this.textBlockOpen) {
      this.textBlockOpen = false;
      this.cfg.emit({ type: 'agent_text_end' });
    }
    this.turnActive = false;
    this.cancelRequested = false;
    this.cfg.emit({ type: 'turn_end', ...end });
    if (this.reloadPending) {
      this.reloadPending = false;
      this.closeQuery();
    }
  }

  /** Translates SDK events into palette events (spec §5 "Streaming ra UI"). */
  onMessage(msg: SDKMessage): void {
    switch (msg.type) {
      case 'system':
        if (msg.subtype === 'init') {
          this.sessionId = msg.session_id;
          this.gotInit = true;
          this.cfg.emit({ type: 'session_info', session_id: msg.session_id, model: msg.model });
          const auth = msg.apiKeySource === 'ANTHROPIC_API_KEY' ? 'API key' : 'Claude login';
          this.initInfo = `${msg.model} · ${auth}`;
          this.cfg.log(
            `init session=${msg.session_id} model=${msg.model} tools=${msg.tools.length} skills=[${(msg.skills ?? []).join(', ')}]`,
          );
          this.cfg.emit({ type: 'ready', info: this.initInfo, version: this.cfg.version });
        }
        return;

      case 'stream_event': {
        if (msg.parent_tool_use_id) return; // subagent output is not shown
        const ev = msg.event;
        if (ev.type === 'content_block_delta' && ev.delta.type === 'text_delta') {
          this.textBlockOpen = true;
          this.cfg.emit({ type: 'agent_text', delta: ev.delta.text });
        } else if (ev.type === 'content_block_stop' && this.textBlockOpen) {
          this.textBlockOpen = false;
          this.cfg.emit({ type: 'agent_text_end' });
        }
        return;
      }

      case 'assistant':
        if (msg.error && msg.parent_tool_use_id === null) {
          this.cfg.emit({ type: 'agent_error', message: ERROR_TEXT[msg.error] ?? `Lỗi từ Claude: ${msg.error}` });
        }
        return;

      case 'rate_limit_event': {
        const i = msg.rate_limit_info;
        this.cfg.emit({
          type: 'rate_limit',
          status: i.status,
          ...(typeof i.utilization === 'number' ? { utilization: i.utilization } : {}),
          ...(typeof i.resetsAt === 'number' ? { resets_at: i.resetsAt } : {}),
          ...(i.rateLimitType ? { limit_type: i.rateLimitType } : {}),
        });
        return;
      }

      case 'result': {
        if (this.staleResults > 0) {
          this.staleResults--; // result of an abandoned turn
          return;
        }
        if (!this.turnActive) return;
        const u = ((msg as unknown as { usage?: Record<string, number> }).usage ?? {}) as Record<string, number>;
        const usage = {
          input_tokens: (u.input_tokens ?? 0) + (u.cache_read_input_tokens ?? 0) + (u.cache_creation_input_tokens ?? 0),
          output_tokens: u.output_tokens ?? 0,
          ...(typeof msg.total_cost_usd === 'number' ? { session_cost_usd: msg.total_cost_usd } : {}),
        };
        this.pending = undefined;
        if (this.cancelRequested) {
          this.finishTurn({ cancelled: true, usage });
        } else if (msg.subtype !== 'success') {
          const why =
            msg.subtype === 'error_max_turns' ? 'Lượt quá dài (vượt số bước tối đa).' :
            msg.subtype === 'error_max_budget_usd' ? 'Vượt ngân sách chi phí của lượt.' :
            'Lỗi khi thực thi lượt.';
          this.finishTurn({ error: why, usage });
        } else {
          this.finishTurn({ usage });
        }
        return;
      }

      default:
        return;
    }
  }
}
