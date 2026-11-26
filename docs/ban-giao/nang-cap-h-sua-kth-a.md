# Bàn giao: Sửa KTH phần A (H4: KTH-09 → 14, prompt: KTH-27) (26/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (giữ nhánh tích hợp). Sửa ngay trên `full/nang-cap-h-tong-the` (từ `aa5753c`), theo báo
cáo `docs/ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md` (lấy từ nhánh luồng chính @ `7e9b92d`). Không đụng `agent/src/ipc/rpc.ts` và
phần H1 / H2 / H3 / H5. Chưa thử trên AutoCAD thật.

## 1. Lỗi đã sửa

| Mã | Mức | Sửa | Test |
| --- | --- | --- | --- |
| KTH-09 | vừa | Lúc học, các kiểu mặc định của AutoCAD (chữ: Standard / Annotative; kích thước: Standard / ISO-25 / Annotative; ghi chú: Standard / Annotative) chỉ được nhận khi mẫu thật sự dùng: có đối tượng (model hoặc layout) dùng, đang là kiểu hiện hành, hoặc là kiểu chữ của một kiểu kích thước / ghi chú được giữ. Ghi chú số kiểu đã bỏ. Soát theo chuẩn công ty báo chữ kiểu Standard như checklist C1 | `SuaKthATests.Autocad_default_styles_are_the_companys_only_…` |
| KTH-10 | vừa | `setup_standards company` trả tối đa 50 `kept_handles` mỗi lượt, khớp giới hạn của thẻ và schema; `kept_more` cho số còn lại, kèm lời dặn hỏi tiếp | `Layers_that_differ_are_asked_fifty_at_a_time` |
| KTH-11 | vừa | `create_sheet company` không trả `title_fields` kiểu VN nữa. Kết quả có `filled` (tên ô của khung tên công ty → chữ đã ghi thật), `empty_fields`, `unknown_attributes` (tên ô không có), và `fields_note` khi Claude truyền `fields` kiểu VN | `A_company_sheet_reports_its_own_fields_as_written` |
| KTH-12 | nhẹ | `HasLayer` miễn layer `KHUNG` / `KHUNG-VP` (`F2Numbers.FrameLayer` / `ViewportLayer`), là chỗ `create_sheet company` đặt khung tên và khung nhìn | `The_frame_and_viewport_layers_…` |
| KTH-13 | nhẹ | (a) Tên có chữ ngoài a–z (chữ Trung, Nhật, Thái…) thêm mã 8 ký tự vào tên thư mục: "株式会社" và "東京建築" thành hai thư mục. Tên Việt giữ thư mục cũ. (b) Hai tên chung thư mục mà khác nhau ngoài hoa / thường / dấu / khoảng trắng / `-_.` (vd "C++" và "C#", "Đ&Đ" và "D D"): `Load` từ chối, kể tên bộ đang ở thư mục đó; học tên mới vào thư mục đó cũng bị từ chối, không còn thay bộ của công ty khác | `Names_without_latin_letters_…`, `A_name_sharing_the_folder_…` |
| KTH-14 | nhẹ | Đường lui (mất bản chép mẫu): cỡ cố định của kiểu chữ dựng lại được nhân `s.MmPerUnit / mm của bản vẽ`. Ghi chú đơn vị nói rõ kiểu chép từ mẫu giữ cỡ theo đơn vị mẫu, chỉ cỡ chữ liệt kê và kiểu dựng lại là đã đổi | `The_fixed_height_of_a_text_style_…` |
| KTH-27 | nhẹ | Prompt mục 7: "Never touch files other than the open drawing, except through the tools made for it when the user asks (export_drawing, compare_drawings, and on 2D the company standard and many-files tools; writing over a file needs their card)". Không ghi tên `company_standard` / `batch_files` vào prompt chung vì gói 3D không có hai lệnh này (test chéo gói) | `The_prompt_lets_the_file_tools_touch_other_files`; test chéo gói của trợ lý |

## 2. Mục nghi vấn H4 trong báo cáo

| Nghi vấn | Kết quả |
| --- | --- |
| Khung nhìn của layout mẫu: coi khung nhìn đầu tiên là khung nhìn của layout | **Sửa** (rẻ): như G2, dùng `Layout.GetViewports()[0]` khi AutoCAD có danh sách; không có (layout chưa mở) thì chỉ bỏ khung nhìn to bằng cả tờ giấy |
| Chọn khung tên: lấy block có thuộc tính đầu tiên (có thể nhận nhầm dấu sửa đổi) | **Sửa** (rẻ): sắp các khung tên theo số ô nhiều nhất trước, nên `create_sheet` lấy khung tên thật |
| Khung tên là xref / lồng; giá trị điền sẵn trong mẫu không mang sang | Không sửa: cần cách nhận khung tên khác hẳn. Ghi để chủ dự án xem khi thử |
| Mẫu DXF lưu bản chép bằng `SaveAs("mau.dwg.tmp")` | Không sửa: cần thử trên AutoCAD thật (checklist H4) |
| Checklist F1 "học từ thư mục mạng" bị luật đường dẫn từ chối nếu bản vẽ đang mở không cùng chỗ | Đúng luật G5 (bảo mật), không sửa; checklist F1 làm với bản vẽ đang mở trong cùng thư mục mạng |
| `create_drawing_set` chưa có `company` | Không làm (tính năng mới, chủ dự án quyết) |

## 3. Con số chờ chủ dự án duyệt

- Không có số mới cho khách.
- Mã 8 ký tự trong tên thư mục bộ chuẩn và 50 layer mỗi lượt đều là giới hạn sẵn có của thẻ, không phải số mới.
- Ngưỡng nhận khung nhìn của chính layout là "to bằng 98 % tờ giấy". Đây là số kỹ thuật, khách không thấy.

## 4. Quyết định tự chọn

- Tên Việt / Latin giữ thư mục cũ (không đổi chỗ các bộ đã học); chỉ tên có chữ ngoài a–z mới có mã thêm.
- Hai tên chung thư mục khác nhau ở dấu câu (C++ / C#): **từ chối**, không đổi cách đặt thư mục. Lý do: rẻ, không làm mất bộ đã học, và Claude nói rõ để khách đặt tên khác.
- Kiểu mặc định của AutoCAD mà mẫu không dùng: **không học** (thay vì sửa checklist C1).

## 5. File

- Sửa:
  - `Core/CompanyStandard.cs`: `Slug`, `SameName`, `OtherNameRefusal`, `Load`, `HasLayer`, `KeepStyle`;
  - `Drawing/CompanyStandardTools.cs`: học mẫu (kiểu đang dùng, khung nhìn, thứ tự khung tên), `kept_handles`, cỡ chữ ở đường lui, ghi chú đơn vị;
  - `Drawing/Standards2DTools.cs`: kết quả của `create_sheet company`;
  - `agent/src/agent/systemPrompt.ts` (một dòng);
  - `docs/checklist-dot-h4.md` và khối H4 trong `docs/checklist-tong-the.md`: thêm F9 → F13.
- Mới:
  - `Core.Tests/SuaKthATests.cs` (11 test);
  - bàn giao này.

## 6. Test

- Lõi **1590 / 1590**, trợ lý **436 / 436** (trước khi gộp hai nhánh kia).
- Build 3 target, CI và các bước gộp: xem `docs/ban-giao/test-tong-the-dot-h-phan-1.md` mục "Sau kiểm tra độc lập".
