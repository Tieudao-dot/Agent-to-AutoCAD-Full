/**
 * Nâng cấp đợt H9: the English of the texts the assistant shows the user (key = the Vietnamese text in the code). A new
 * text passed to tr() needs its line here (test/nangcaph9.test.ts lists the missing ones).
 */
export const EN: Readonly<Record<string, string>> = {
  '{0} đối tượng': '{0} objects',
  'Không khởi tạo được phiên Claude: {0}': 'Could not start the Claude session: {0}',
  'Không khôi phục được cuộc chat cũ của bản vẽ này; tớ bắt đầu cuộc chat mới.': 'Could not restore the old chat of this drawing; I started a new chat.',
  'Phiên Claude bị lỗi: {0}': 'The Claude session failed: {0}',
  'Tiến trình Claude kết thúc bất thường. Gửi lại yêu cầu để tiếp tục.': 'The Claude process ended unexpectedly. Send the request again to continue.',
  'Lỗi từ Claude: {0}': 'Error from Claude: {0}',
  'Lượt quá dài (vượt số bước tối đa).': 'The turn is too long (more than the maximum number of steps).',
  'Vượt ngân sách chi phí của lượt.': 'The turn\'s cost budget was exceeded.',
  'Lỗi khi thực thi lượt.': 'Error while running the turn.',
  'Claude trả về lỗi API.': 'Claude returned an API error.',
  'Chưa đăng nhập Claude. Bấm nút "Đăng nhập Claude" bên dưới và đăng nhập tài khoản Pro/Max trên trình duyệt (hoặc đặt biến môi trường ANTHROPIC_API_KEY).': 'Not signed in to Claude. Press the "Sign in to Claude" button below and sign in to a Pro/Max account in the browser (or set the ANTHROPIC_API_KEY environment variable).',
  'Tài khoản Claude này không được phép dùng Claude Code.': 'This Claude account is not allowed to use Claude Code.',
  'Lỗi thanh toán / hết hạn mức tài khoản Claude.': 'Billing error / the Claude account\'s limit is used up.',
  'Đã chạm giới hạn sử dụng của gói Claude. Đợi một lúc rồi thử lại.': 'The Claude plan\'s usage limit is reached. Wait a while and try again.',
  'Máy chủ Claude đang quá tải, thử lại sau ít phút.': 'The Claude servers are overloaded, try again in a few minutes.',
  'Model không khả dụng với tài khoản này. Đặt CLAUDECAD_MODEL sang model khác (ví dụ claude-sonnet-5-5).': 'The model is not available for this account. Set CLAUDECAD_MODEL to another model (e.g. claude-sonnet-5-5).',
  'Câu trả lời quá dài và bị cắt.': 'The answer was too long and was cut.',
  'Tài khoản Claude đang bị tạm khóa. Đăng nhập claude.ai để xem chi tiết, hoặc đổi tài khoản trong ⚙ ▸ Tài khoản AI.': 'The Claude account is on hold. Sign in at claude.ai for the details, or switch accounts in ⚙ ▸ AI account.',
  'Tài khoản Claude cần xác minh. Đăng nhập claude.ai trên trình duyệt để hoàn tất, rồi thử lại.': 'The Claude account needs verification. Sign in at claude.ai in the browser to finish it, then try again.',
  'Máy chủ Claude gặp lỗi tạm thời, thử lại sau ít phút.': 'The Claude servers had a temporary error, try again in a few minutes.',
  'Yêu cầu gửi tới Claude không hợp lệ (có thể do ảnh hoặc tệp đính kèm quá lớn). Bớt tệp đính kèm rồi thử lại.': 'The request sent to Claude is not valid (maybe an image or attachment is too large). Remove some attachments and try again.',
  'Lỗi thông tin xác thực của nhà cung cấp đám mây cấu hình cho Claude.': 'Credential error of the cloud provider set up for Claude.',
  'Claude gặp lỗi không rõ nguyên nhân. Thử lại; nếu lặp lại, bấm ⚙ ▸ Gửi báo cáo lỗi.': 'Claude hit an unknown error. Try again; if it repeats, press ⚙ ▸ Send bug report.',
  '{0} lệnh OK': '{0} commands OK',
  '{0} lỗi': '{0} errors',
  'xóa {0}': 'erased {0}',
  '{0} kết quả': '{0} results',
  'ảnh {0}×{1}': 'image {0}×{1}',
};
