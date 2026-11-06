# Skill ClaudeCAD

Skill là chuẩn vẽ của cậu (bảng layer, chiều cao chữ, quy cách, quy trình) viết thành file Markdown. Claude tự nạp skill phù hợp với yêu cầu và vẽ theo đúng chuẩn đó.

## Thêm skill ngay trên khung chat (v0.5.0)

1. Soạn một file `.md` (Notepad, VS Code… — lưu **UTF-8**) bắt đầu bằng phần đầu:

   ```markdown
   ---
   name: ve-ket-cau
   description: Chuẩn vẽ kết cấu của công ty - layer S-*, dầm, cột, móng. Dùng khi được yêu cầu vẽ kết cấu, dầm, cột, móng, sàn, thép.
   ---

   # Vẽ kết cấu theo chuẩn
   ...
   ```

   - `name`: chữ thường không dấu, số, gạch nối. Bỏ trống thì plugin lấy từ tên file ("Vẽ Kết Cấu.md" → `ve-ket-cau`).
   - `description` (bắt buộc, 20–1024 ký tự): **khi nào dùng** skill + từ khóa. Claude dựa vào dòng này để quyết định nạp skill.
2. Trên khung chat bấm **+ Add skill** → chọn file (chọn được nhiều file). Hộp chọn mặc định mở thư mục skill của plugin.
3. Khung chat báo **"✓ Đã thêm skill ve-ket-cau"** (hoặc lý do không thêm được). Skill bật ngay; Claude dùng từ tin nhắn tiếp theo mà vẫn nhớ cuộc chat.
4. Trùng tên với skill đã có → plugin hỏi có thay không. Chọn lại một file đã nằm trong thư mục plugin → chỉ bật lại skill đó.

## Quản lý: nút **Skill (N)**

Danh sách mọi skill trong plugin với trạng thái (skill mẫu / đã thêm ngày … / đang bật / đã tắt / lỗi):

- **☑ / ☐** bật hoặc tắt (skill tắt không được nạp).
- **Sửa** mở `SKILL.md` bằng trình soạn mặc định; lưu file là plugin tự nạp lại.
- **Xóa** xóa thư mục skill (có hỏi lại).
- **Mở thư mục** mở thư mục skill trong Explorer.

Ô **Chuẩn** trên khung chat tự liệt kê các skill đang bật để chọn cố định cho mọi yêu cầu (hoặc để **Tự động**).

## Lưu ở đâu

| | Đường dẫn |
| --- | --- |
| Thư mục skill của plugin | `%AppData%\Autodesk\ApplicationPlugins\ClaudeCAD.bundle\Contents\skills\` |
| Mỗi skill | `...\skills\<tên>\SKILL.md` (+ file tham khảo nếu skill là một thư mục) |
| File lưu danh sách skill đã thêm | `...\skills\skills.json` — tên, bật/tắt, ngày thêm, file nguồn |

Chọn một file tên `SKILL.md` thì plugin chép cả các file `.md/.txt/.csv/.json` cùng thư mục (file tham khảo). Cài bản cập nhật không ghi đè skill và `skills.json` của cậu.

## Skill mẫu có sẵn

| Skill | Dùng cho |
| --- | --- |
| `ve-kien-truc` | mặt bằng kiến trúc: trục, cột, tường 220/110, cửa, kích thước, tên phòng |
| `ve-mep` | điện (chiếu sáng, ổ cắm, máng cáp) và cấp thoát nước |
| `ve-ha-tang` | đường, lý trình, cao độ, cống, ranh giới, bảng tọa độ |

Nên có mục **"Quy trình chuẩn bị"** nhắc Claude gọi `get_drawing_info` + `list_layers` trước, tạo layer/style còn thiếu theo bảng chuẩn, rồi mới vẽ. Xem `ve-kien-truc/SKILL.md` làm mẫu.
