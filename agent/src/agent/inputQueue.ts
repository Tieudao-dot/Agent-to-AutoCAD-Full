/**
 * Push-based AsyncIterable used as the streaming input of query():
 * the sidecar pushes a user message whenever the palette sends one.
 */
export class InputQueue<T> implements AsyncIterable<T> {
  private readonly items: T[] = [];
  private waiters: Array<(r: IteratorResult<T>) => void> = [];
  private closed = false;

  push(item: T): void {
    if (this.closed) throw new Error('queue closed');
    const w = this.waiters.shift();
    if (w) w({ value: item, done: false });
    else this.items.push(item);
  }

  close(): void {
    if (this.closed) return;
    this.closed = true;
    for (const w of this.waiters) w({ value: undefined as never, done: true });
    this.waiters = [];
  }

  get isClosed(): boolean {
    return this.closed;
  }

  [Symbol.asyncIterator](): AsyncIterator<T> {
    return {
      next: () => {
        const item = this.items.shift();
        if (item !== undefined) return Promise.resolve({ value: item, done: false });
        if (this.closed) return Promise.resolve({ value: undefined as never, done: true });
        return new Promise((resolve) => this.waiters.push(resolve));
      },
      return: () => {
        this.close();
        return Promise.resolve({ value: undefined as never, done: true });
      },
    };
  }
}
