# Skill ClaudeCAD

Skill là chuẩn vẽ của cậu (bảng layer, chiều cao chữ, quy cách, quy trình) viết thành file Markdown. Claude tự nạp skill phù hợp với yêu cầu và vẽ theo đúng chuẩn đó.

## Skill theo từng đoạn chat (v0.9.4)

Mỗi đoạn chat (mỗi bản vẽ) dùng **bộ skill riêng**, lấy từ **thư viện skill**: tất cả skill cậu đã thêm từ trước đến giờ.

**Thêm skill: nút + Add skill**

1. Soạn file `.md` (Notepad, VS Code… — lưu **UTF-8**) bắt đầu bằng:

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
2. Bấm **+ Add skill** → chọn **một hoặc nhiều file** cùng lúc (giữ Ctrl / Shift để chọn nhiều).
3. Skill vào **thư viện** và **được dùng luôn trong đoạn chat hiện tại**. Khung chat báo "✓ Đã thêm skill …". Claude dùng từ tin nhắn tiếp theo, vẫn nhớ cuộc chat.
4. Trùng tên với skill đã có → plugin hỏi có thay không.

**Danh sách skill: nút Skill (N)** (N = số skill đoạn chat này đang dùng)

- **ĐANG DÙNG TRONG ĐOẠN CHAT NÀY:** skill có dấu ✓ và nút **−**. Bấm **−** thì đoạn chat này không dùng skill đó nữa; skill vẫn nằm trong thư viện. Claude được dặn bỏ qua chuẩn của skill đó từ tin nhắn tiếp theo; muốn sạch hoàn toàn khỏi trí nhớ cuộc chat thì bấm **Chat mới**.
- **THƯ VIỆN (chưa dùng trong đoạn chat này):** các skill còn lại, có nút **+** để dùng trong đoạn chat này.
- **Sửa:** mở `SKILL.md` bằng trình soạn mặc định. Lưu file là plugin tự nạp lại.

**Đoạn chat mới** (bấm Chat mới, hoặc mở bản vẽ chưa có chat) dùng **đúng các skill của đoạn chat gần nhất**. 3 skill mẫu đi kèm plugin có sẵn trong thư viện nhưng **không tự dùng**: bấm **+** khi cần.

Muốn bỏ hẳn một skill khỏi thư viện: xóa thư mục `<tên skill>` trong thư mục skill (đường dẫn ở bảng dưới).

## Lưu ở đâu

| | Đường dẫn |
| --- | --- |
| Thư mục skill của plugin | `%AppData%\Autodesk\ApplicationPlugins\ClaudeCAD.bundle\Contents\skills\` |
| Mỗi skill | `...\skills\<tên>\SKILL.md` (+ file tham khảo nếu skill là một thư mục) |
| File lưu danh sách skill đã thêm | `...\skills\skills.json` — tên, ngày thêm, file nguồn |
| Skill của từng đoạn chat | lưu cùng lịch sử chat của bản vẽ (`%LocalAppData%\ClaudeCAD\chats`); skill của đoạn chat gần nhất trong `%AppData%\ClaudeCAD\settings.json` |

Chọn một file tên `SKILL.md` thì plugin chép cả các file `.md/.txt/.csv/.json` cùng thư mục (file tham khảo). Cài bản cập nhật không ghi đè skill và `skills.json` của cậu.

## Skill mẫu có sẵn

| Skill | Dùng cho |
| --- | --- |
| `ve-kien-truc` | mặt bằng kiến trúc: trục, cột, tường 220/110, cửa, kích thước, tên phòng |
| `ve-mep` | điện (chiếu sáng, ổ cắm, máng cáp) và cấp thoát nước |
| `ve-ha-tang` | đường, lý trình, cao độ, cống, ranh giới, bảng tọa độ |

Nên có mục **"Quy trình chuẩn bị"** nhắc Claude gọi `get_drawing_info` + `list_layers` trước, tạo layer/style còn thiếu theo bảng chuẩn, rồi mới vẽ. Xem `ve-kien-truc/SKILL.md` làm mẫu.
