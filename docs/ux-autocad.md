# Giao diện theo phong cách AutoCAD

Quyết định thiết kế cho khung chat ClaudeCAD: trông và cư xử như một palette gốc của AutoCAD
(Properties, Layer Properties), thay vì một ứng dụng chat kiểu web đặt trong AutoCAD.

Màn mẫu: artifact "ClaudeCAD — giao diện mẫu UX" (A1 khung chat, A2 cài đặt, B xác nhận,
C bắt đầu + lỗi, D trong lúc làm, E icon thanh trạng thái, F thu gọn).

## 1. Màu và font lấy từ AutoCAD

AutoCAD 2022 (24.1) và 2025 (25.0) đều có `Autodesk.Windows.Palettes.PaletteTheme`
(AdUiPalettes.dll), đọc qua `Autodesk.Windows.Themes.ThemeManager.Instance.CurrentPaletteTheme`.

`UI/AcadTheme.cs` đọc bằng **reflection** — cố ý, vì plugin phải build và nạp được khi không có
assembly đó (csproj chỉ tham chiếu acmgd/acdbmgd/accoremgd/AdWindows ở chế độ Install, và UI test
chạy không có AutoCAD). Thuộc tính nào thiếu thì khóa màu đó giữ giá trị dựng sẵn trong `UI/Theme.cs`.

| Khóa trong `Theme.cs` | Thuộc tính PaletteTheme (thử theo thứ tự) |
| --- | --- |
| `PanelBg`, `AssistantBg` | `PaletteContainerBackgroundColor`, `PaletteBackgroundColor` |
| `HeaderBg`, `CardBg` | `CategoryBackgroundColor`, `PropertyInspectorCategoryBackgroundColor` |
| `InputBg`, `CodeBg` | `TextInputBackgroundColor`, `TextInputBackgroundBrush` |
| `Fg` | `ControlTextColor`, `PaletteTextActiveColor` |
| `MutedFg` | `TextInputInactiveTextColor`, `ListBoxInactiveTextColor` |
| `Border` | `CategoryBorderColor`, `TextInputBorderColor` |
| `UserBg`, `ChipBg` | `SquareButtonBackgroundColor`, `CategoryBackgroundColor` |
| `HoverBg` | `SquareButtonHoverBackgroundBrush`, `ListBoxHoverBackgroundBrush` |
| `PressBg` | `SquareButtonClickBackgroundBrush`, `ListBoxSelectBackgroundBrush` |
| `Accent` | `TextInputActiveBorderColor`, `ListBoxSelectBorderColor` |
| `LinkFg` | `PaletteHyperlinkTextColor` |
| Font | `NormalFont` → `FontFamily`, `FontSize`, `FontWeight`, `FontStyle` |

Quy tắc:

- Màu trong suốt (`A == 0`) coi như "không đặt" → giữ màu dựng sẵn, tránh chữ vô hình.
- Cỡ chữ ngoài khoảng 8–24 bị bỏ qua (đề phòng point vs pixel).
- Cỡ chữ theo AutoCAD khi `PaletteSettings.FontAuto` còn bật; người dùng tự chọn cỡ → tắt hẳn.
- Người dùng ép tối/sáng trái với AutoCAD → chỉ lấy **font**, không lấy màu (`Info.FontOnly()`).
- Đổi theme: sự kiện `ThemeManager.PropertyChanged`; dự phòng là biến hệ thống `COLORTHEME`
  (0 = tối, 1 = sáng) đã dùng từ trước.

## 2. Lớp vỏ palette

- Góc vuông (0–2 px) thay vì bo 4/6/10 px; không bong bóng chat — tin nhắn là khối phẳng toàn
  chiều rộng, có nhãn nhỏ "CẬU" / "CLAUDE" và vạch mảnh phân cách.
- Thanh công cụ là hàng icon 16 px đơn sắc, mỗi nút có tooltip giải thích ngắn gọn.
- Các mục dài (Cài đặt, Skill, bước đang làm) là tiêu đề nhóm thu gọn được, như nhóm
  General / Geometry trong Properties.
- Màu nhấn chỉ ở nút chính của mỗi thẻ và ô đang nhập, không rải khắp palette.
- Shadow mảnh ở thanh công cụ, thanh nhập và các thẻ để tách khỏi nền model space.
- Cài đặt dựng theo lưới 2 cột (tên bên trái, giá trị bên phải) như palette Properties.
- Lỗi và cảnh báo dùng icon ⊗ / ⚠ + chữ thường + nút xử lý, không tô nền đỏ cả thẻ.

## 3. Điểm vào trong AutoCAD

| Điểm vào | Trạng thái | Ghi chú |
| --- | --- | --- |
| Icon thanh trạng thái + bong bóng | Đã làm (`UI/StatusTray.cs`) | `Application.StatusBar.TrayItems`, `TrayItem.ShowBubbleWindow`: xám = chưa kết nối, thường = sẵn sàng, chấm xanh = đang làm, chấm vàng = cần xác nhận. Bong bóng chỉ hiện khi palette thu gọn / đóng; tắt được trong Cài đặt. |
| Menu chuột phải "Hỏi Claude về đối tượng đã chọn" | Không làm đợt này | `Application.AddObjectContextMenuExtension` có sẵn nếu sau này cần. |
| `CC <yêu cầu>` + in kết quả ở command line | Không làm đợt này | `Editor.WriteMessage` / `GetString`. |
| Tab ribbon | Bỏ | `ComponentManager.Ribbon` có sẵn nhưng chiếm chỗ ribbon, không làm. |

## 4. Quy ước thao tác

- Esc trong ô nhập = Dừng lượt; Esc trong bản vẽ vẫn là của AutoCAD.
- Enter gửi, Shift+Enter xuống dòng.
- "Xem trên bản vẽ" dùng đúng cơ chế chọn đối tượng (grip) của AutoCAD; Esc bỏ chọn.
- Nút hoàn tác ghi rõ "tương đương 1 lần U".
- Thuật ngữ giữ tiếng Anh của AutoCAD (Layer, Block, Hatch, Dim, Polyline, Xref) trong câu tiếng Việt.
- Số đo theo đơn vị bản vẽ ("tường 220"), tọa độ kiểu `(12000, 8500)`.
- Giữ nguyên neo trái/phải, auto-hide, đổi cỡ của palette; icon palette đổi theo theme
  (`PaletteSet.SetThemedIcon`).

## 5. Nguồn

- [Biến COLORTHEME — Autodesk Help 2022](https://help.autodesk.com/cloudhelp/2022/ENU/AutoCAD-LT/files/GUID-64D9B2CD-E6C6-423B-8AA9-A876CF9502C0.htm)
- [Lớp PaletteSet — AutoCAD .NET Reference](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_Windows_PaletteSet.html)
- Thư viện tham chiếu NuGet `AutoCAD.NET` 24.1.51000 và 25.0.1 (AdUiPalettes, AcMgd, AdWindows):
  các thuộc tính và API trong bảng trên đều có ở cả hai bản.
- Nền model space tối mặc định RGB 33, 40, 48 (tài liệu Autodesk).

## 6. Tooltip

Mỗi nút icon và mỗi nút hành động đều có tooltip dạng **"Tên — giải thích ngắn"**, ví dụ
"Chat mới — mở đoạn chat mới cho bản vẽ này, đoạn cũ vẫn xem lại được". Nút icon không đủ chỗ ghi
số lượng, nên khi có nội dung (Macro đã lưu, Phiên bản, Ghi chú) icon chuyển sang màu nhấn và số
lượng nằm trong tooltip — giống nút đang bật trong AutoCAD.

## 7. Thu gọn (màn F)

Nút ⌄ trên thanh công cụ co palette lại tại chỗ: chỉ còn hàng công cụ, một dòng trạng thái và ô nhập.

- **Không tải lại gì**: phần chat chỉ ẩn (`Visibility`), mở rộng là thấy nguyên đoạn chat của phiên.
- **Enter gửi ngay**, Shift+Enter xuống dòng — như khi mở rộng.
- **Dòng trạng thái** khi đang thu gọn: "● Đang làm · … · N thao tác" lúc chạy; "✓ Đã xong: <dòng đầu câu trả lời>
  · bấm để xem" khi xong; "⊗ …" khi lỗi / hết hạn mức. Bấm dòng đó hoặc ⌃ để mở rộng.
- **Tự mở rộng chỉ khi cần cậu quyết**: thẻ xác nhận, thẻ kế hoạch, đăng nhập, kích hoạt. Lỗi thường chỉ báo trên dòng.
- **Kích thước**: palette nổi co chiều cao còn ~170 px và nhớ chiều cao cũ để trả lại; palette neo giữ cột của
  AutoCAD (AutoCAD quản kích thước cột neo), ô nhập chuyển lên sát thanh công cụ.
- Trạng thái thu gọn không lưu qua lần mở AutoCAD sau (theo lựa chọn của chủ plugin).

## 8. Gửi thêm ý khi Claude đang làm

Tin gõ lúc Claude bận được **xếp hàng** ở plugin và gửi khi lượt kết thúc sạch; nó không chen vào giữa lượt.
Lý do: sidecar và cổng xác nhận tính "một tin = một lượt" (đếm đối tượng bị ảnh hưởng, câu đồng ý/từ chối,
ghép kết quả với lượt). Chèn tin giữa lượt sẽ làm lệch các phép tính an toàn đó. Sau lỗi, hết hạn mức, Dừng,
hoặc khi lượt vừa hỏi "đồng ý?", chữ được trả về ô nhập để người dùng tự quyết — một tin chờ không bao giờ
tự trả lời hộ một câu hỏi xác nhận.

## 9. Hàng icon dưới ô nhập (màn A1)
Bốn nút đính kèm dưới ô nhập dùng cùng `IconButton` với thanh công cụ: ⬚ Chọn đối tượng, ✛ Lấy điểm,
▨ Ảnh, ⛶ Chụp. Tooltip theo mẫu "Tên — giải thích ngắn". Nút **Gửi** vẫn là nút chữ, vì đó là hành động chính
và nhãn của nó đổi thành "Gửi thêm" khi Claude đang làm. Các glyph có trong font Segoe UI Symbol của Windows;
nếu máy nào hiện ô vuông trống thì ghi vào test-log UX-18.

## 10. Thẻ "Bắt đầu" (màn C)
`AddWelcomeCard` giữ nguyên chữ ký (danh sách kiểm tra + cần đăng nhập không) nên `ChatController.ShowWelcome`
không đổi. Thẻ có thanh tiêu đề như nhóm Properties và ba hàng `WelcomeStep`: *done* (✓ màu `Theme.OkFg`),
*now* (số màu nhấn, có nút), *later* (mờ). Kích hoạt lấy từ dòng "Kích hoạt", đăng nhập từ dòng "Tài khoản Claude";
các dòng còn lại (Node.js, trợ lý, skill) chỉ hiện ⊗ khi thiếu, đủ thì gộp thành một dòng chữ nhỏ cuối thẻ.
Bước 3 không tự gửi: *Điền vào ô nhập* chỉ đưa câu mẫu vào ô để người dùng xem rồi Enter.
