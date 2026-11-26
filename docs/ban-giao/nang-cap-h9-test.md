# Bàn giao: Test & sửa đợt H9: giao diện tiếng Anh (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người rà). Nhánh `full/nang-cap-h9-test`, tách từ `full/nang-cap-h9` @ `0dad81d`
(= `60a2047` + bàn giao). Chưa thử trên AutoCAD thật.

## 1. Lỗi đã sửa

| # | Lỗi | Khách sẽ gặp | Sửa |
| --- | --- | --- | --- |
| H9T-1 (nặng) | Xác nhận gõ tay: dấu nháy cong "don’t" (bàn phím điện thoại / Mac) không bị tách nên "don’t" không là từ phủ định | "yes but don’t delete the walls" được tính là **đồng ý** → xóa | Tách cả ‘ ’ “ ” (`confirmation.ts`) |
| H9T-2 (nặng) | Câu có điều kiện / ngoại lệ được tính là đồng ý: "đồng ý nhưng giữ tường", "ok xóa hết trừ cửa", "yes, except the doors", "can't / won't", "never mind" | Claude làm cả việc khách vừa loại ra | Thêm vào từ phủ định: mọi "n't" (`t`), never / nevermind, but / except / instead, nhưng / trừ, khoan. Câu có điều kiện = không rõ = không đồng ý (luồng chính chốt 26/11) |
| H9T-3 | Máy nâng cấp từ bản trước H9 đã có file cài đặt (chưa có mục ngôn ngữ) nên bỏ qua lựa chọn ở bộ cài | Chọn English ở bộ cài, khung chat vẫn tiếng Việt | `PaletteSettings.LanguageSaved` (file có ghi ngôn ngữ chưa); khung chat dùng ngôn ngữ bộ cài khi chưa có. Ngôn ngữ chọn trong ⚙ vẫn giữ qua mọi lần cài lại |

## 2. Test thêm

- Trợ lý (`agent/test/nangcaph9.test.ts`, +2):
  - câu khó:
    - **không**: "yes but don’t delete", "ok, cancel that", "không, đừng xóa", "ok I can’t say", "sure, never mind",
      "yes, except the doors", "ok but keep the walls", "đồng ý nhưng giữ tường", "ok xóa hết trừ cửa", "đồng ý, khoan đã"…;
    - **có**: "sure, go ahead", "yes, delete it", "có, xóa đi", "go ahead and erase them", "yes please";
  - mọi lời báo lỗi trong bảng `ERROR_TEXT` (`session.ts`, tới `tr()` qua biến nên test cũ không thấy) có bản tiếng Anh.
- Lõi (`NangCapH9Tests.cs`, +1 test, +1 đoạn):
  - **chỉ các file khung chat được gọi `Tr`**: một `Tr` mới trong code vẽ (bảng, khung tên, H6 / H7 / H8…) làm test hỏng,
    vì chữ bản vẽ phải giữ tiếng Việt (rộng hơn danh sách 10 file của test cũ, test cũ vẫn giữ);
  - `LanguageSaved` với file cũ / file mới.
- Lõi 1682 / 1682, trợ lý 466 / 466 (`# fail 0`), build `net48` / `net8.0-windows` / `net10.0-windows` 0 lỗi.

## 3. Đã soát, không phải sửa (theo 7 điểm luồng chính + 9 điểm bước 0)

1. "in Vietnamese" cứng: không còn câu dặn nào ngoài câu mặc định (C# + TS, có test trợ lý).
2. Tên nút trong lời dặn: `ENGLISH_SECTION` có bảng đổi tên nút, nằm cuối prompt, chỉ có khi English.
3. Chữ không được dịch: không file vẽ nào gọi `Tr` (test mới giữ). "cột" / "dầm", "TƯỜNG", "cửa", "Tổng", "·" không đổi.
   `Find("Kích hoạt")` tìm theo tên gốc, hiện `Tr(name)`. "chưa đăng nhập" dò cả bản tiếng Anh.
4. Đổi ngôn ngữ giữa chừng: tin nhắn có ngôn ngữ khác thì trợ lý đóng tiến trình và mở lại **cùng cuộc chat** (resume) với
   prompt mới; khởi động sẵn chỉ đóng tiến trình đang chờ. Tóm tắt log và lời báo lỗi đi qua `tr()` theo ngôn ngữ hiện tại.
5. Bộ cài: tiếng Việt đứng đầu (mặc định), mọi `CustomMessage` có đủ hai thứ tiếng, chỗ trống `%1` / `{#…}` khớp, không
   còn chữ tiếng Việt trong chuỗi Pascal (chỉ trong chú thích). Lời liên hệ mặc định khớp C# (`LangEn`) và `.iss`
   (`ActivationContactEn`). Lời liên hệ riêng trong `update-config.json` giữ như chủ plugin viết.
6. Chỗ trống `{0}`: test C# so tập chỗ trống từng câu; `L.Tr` không vỡ khi bản dịch hỏng (dùng câu tiếng Việt). TS so
   chỗ trống của mọi `tr('…')`.
7. Test bắt chuỗi thiếu bản dịch: không có `Tr($"…")` nào (chuỗi đã ghép thì không tìm được bản dịch).

## 4. Để lại (không sửa, ghi để biết)

- `L.Tr(vi, args)` định dạng số theo văn hóa của Windows: máy Windows tiếng Việt, giao diện English vẫn có thể hiện
  "1.234" thay vì "1,234" ở câu có `{0:N0}`.
- Số ít / số nhiều tiếng Anh: "1 objects", "1 errors" ở log hoạt động.
- Đổi ngôn ngữ khi khung chat đang mở: chữ khớp nguyên văn bảng dịch được đổi cả hai chiều; một tin nhắn cũ của khách đúng bằng
  một chữ giao diện (vd chỉ gõ "Gửi") cũng bị đổi theo. Rất hiếm.
- Dòng ngữ cảnh gửi Claude trong mỗi tin nhắn (`content.ts`, vd "[Người dùng bấm nút Đồng ý…]") vẫn tiếng Việt; Claude
  hiểu, câu trả lời theo mục "# Language".
- Không có test TS bắt chữ tiếng Việt hiện cho khách mà quên `tr()` (TS trộn chữ cho Claude và chữ cho khách trong cùng
  file). Hiện các chỗ hiện cho khách đều đã bọc.

## 5. Con số chờ chủ dự án duyệt

Không có số mới.

## 6. CI

- Lượt 1: ghi ở dưới.

## 7. Chưa kiểm được

- AutoCAD thật; bộ cài thật trên máy nâng cấp (mục B6 checklist); dấu nháy cong gõ từ bàn phím thật (C3b).
