# Bàn giao: Test tổng thể (gói dịch vụ + 3D đợt 1, 2, 3)

*12/11/2026 · luồng chính giao. Nhánh `full/test-tong-the`, xuất phát `full/ve-3d-test-3` @ `4bf5cce` (đã gộp tài liệu
của luồng chính từ `claude/inspiring-cray-58xsmt`, không xung đột). Bước 6, bước cuối trong chuỗi chủ dự án đặt.*

Máy cloud không có AutoCAD: "test" = rà code theo chiều ngang (chỗ gói, macro, cổng xác nhận, self-test và 3 đợt 3D chạm
nhau), viết test tự động quét nguồn để lỗi cùng loại tự đỏ về sau, sửa lỗi. Checklist cho chủ dự án thử tay:
[`../checklist-tong-the.md`](../checklist-tong-the.md).

## Đã rà

| Phần | Kết quả |
| --- | --- |
| Gói × 3D: chặn ở plugin | 29 lệnh `ThreeD` (14 + 12 + 3) bị từ chối ở gói 2D và máy mới (gói mặc định 2D): lệnh lẻ, từng lệnh trong `batch_execute` (kiểm gói **trước** "không chạy trong batch", nên luôn ra `NOT_IN_PLAN`), macro (cả hai đường chạy macro đi qua `HandleAsync`). Gói 3D bị từ chối mọi lệnh nhóm 2D; Full có đủ. Không sửa logic gói |
| Gói × 3D: trợ lý | `plan_tools` lọc đúng: gói 2D không nạp lệnh 3D, gói 3D không nạp lệnh nhóm 2D, Full nạp mọi lệnh. Skill `ve-3d-kien-truc` chỉ nạp ở 3D / Full. Đổi gói giữa chừng: phiên Claude khởi động lại, cùng cuộc chat (test sẵn có trong `session.test.ts`) |
| System prompt theo gói | **Lỗi 1** (bảng dưới) |
| Cổng xác nhận | Đủ 7 lệnh đổi hình khối luôn qua thẻ khi khối có trước lượt (cả trong batch), khối dựng trong lượt thì không; 7 lệnh theo luật > 20; 15 lệnh không bao giờ đổi đối tượng có sẵn. Test mới bắt phân loại mọi lệnh 3D |
| Macro | Mọi tham số handle của mọi lệnh (14 tên ở 3D: `target`, `subtract`, `profile_handles`, `path_handle`, `section_handles`, `guide_handles`, `other_handles`, `wall_handles`, `centerline_handles`, `door_handles`, `window_handles`, `slab_handles`, `handle`, `handles`) được ánh xạ. Không có tham số thường nào trùng dạng tên handle. Test mới quét nguồn |
| Hoàn tác U khi trộn đợt | Mọi lệnh ghi dùng chung một nhóm U của lượt; `set_ucs` (không transaction) cũng mở nhóm đó; `set_view_3d` / `set_visual_style` không mở nhóm. Đúng; thử tay X1 |
| "Chỉ xem" | Chỉ `zoom_*`, `set_view_3d`, `set_visual_style` được đổi; lệnh đọc 3D (`get_solid_info`, `check_interference`) chạy; `set_ucs`, mọi lệnh dựng / sửa, cả `build_3d_from_plan` chạy thử bị chặn. Đúng |
| Giới hạn tham số | Số handle tối đa của mọi lệnh 3D khớp giữa trợ lý và plugin (500 / 200 / 5000 / 50, `MaxSourceEntities` 5000, `MaxSectionSolids` 500); `SOLID_LIMITS` / `SOLID2_LIMITS` / `PLAN3D_LIMITS` đã có test so nguồn |
| Thông báo lỗi | `NOT_IN_PLAN`, `READ_ONLY`, "Confirmation required", `INVALID_PARAMS` "Nothing was changed / built": cùng mẫu ở 3 đợt |
| `CLAUDECHATSELFTEST` | 7 bước 3D chạy nối tiếp trên bản vẽ tạm, khối đặt ở các vùng x tách nhau (6000, 9000, 12000, 15000, 20000) nên không đè; mỗi bước transaction riêng; bản vẽ tạm đóng không lưu, thư mục thử xóa khi mọi bước đạt. Không thấy lỗi |
| Bản 2D | Test 2D cũ xanh; system prompt gói 2D và Full giữ nguyên nội dung 2D (chỉ đổi 1 câu ở mục 4, xem lỗi 1) |

## Lỗi tìm được và cách sửa

| # | Mức | Lỗi | Sửa |
| --- | --- | --- | --- |
| 1 | Vừa | System prompt gói **3D** bảo Claude dùng lệnh gói đó không có: "Practical geometry hints" (tường bằng `offset_entity`, chuỗi kích thước `add_dim_linear` + `add_dim_continue`) và câu "đặt `create_text_style` đầu batch". Claude bị dẫn tới lệnh sẽ trả `NOT_IN_PLAN`, tốn lượt và trái với câu "không bắt chước lệnh thiếu" | Tách `TWO_D_HINTS`, chỉ thêm cho gói 2D / Full (và plugin cũ không gửi gói); câu batch đổi thành "create_layer (và kiểu cần dùng)". Test mới: system prompt của mỗi gói không nhắc tên lệnh nào ngoài gói |

Logic gói: **không sửa**. Không đổi `activation/activation.json`, `License.cs`, `.claude/settings.json`.

## Test thêm (để sau này thêm lệnh mà quên thì đỏ)

- `plugin/ClaudeCAD.Core.Tests/CrossPlanTests.cs` (5 test):
  - quét `agent/src/tools/*.ts` (tham số kiểu `handle` / `handles`) và `Drawing/*.cs` (`p.Handle(s)`, `Explicit`): mọi
    tham số handle phải được macro ánh xạ; plugin không đọc handle từ tham số mà trợ lý không khai là handle; không có
    tham số thường mang tên dạng handle;
  - 29 lệnh 3D bị chặn ở gói 2D / máy mới, lệnh nhóm 2D bị chặn ở gói 3D, Full = mọi lệnh;
  - cổng gói chạy ở lệnh lẻ và từng lệnh batch (trước kiểm "không chạy trong batch"), macro đi qua `HandleAsync`.
- `agent/test/crossplan.test.ts` (6 test): lệnh nạp theo gói; system prompt không nhắc lệnh ngoài gói; skill 3D chỉ ở
  3D / Full; mọi lệnh `ThreeD` thuộc đúng một lớp của cổng xác nhận (luôn hỏi / theo số lượng / không đổi gì).

## Test và build

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **309/309** (304 + 5).
- Trợ lý `npm test`: **191/191** (185 + 6).
- Build Linux (.NET SDK 10, xóa `obj` / `bin` giữa các lần): net48, net8.0-windows, net10.0-windows: 0 lỗi, 0 cảnh báo.
- CI Windows: xem mục Tiến độ.

## Số token theo gói

Đo bằng script: dựng danh sách lệnh của từng gói đúng như plugin gửi (`plan_tools`), đổi schema sang JSON như SDK gửi
cho Claude, cộng system prompt. Không có bộ đếm token chính xác trên máy cloud; ước theo ~3,5 ký tự / token (khoảng
3–4).

| Gói | Số lệnh (kể cả 5 lệnh thẻ) | Mô tả lệnh (ký tự) | System prompt (ký tự) | Tổng (ký tự) | ≈ token |
| --- | --- | --- | --- | --- | --- |
| 2D | 95 | 116 214 | 10 086 | 126 300 | ~36k |
| 3D | 90 | 124 486 | 15 415 | 139 901 | ~40k |
| Full | 124 | 174 504 | 15 213 | 189 717 | ~54k |
| Riêng phần 3D (29 lệnh + luật 3D) | 29 | 58 290 | 5 452 | 63 742 | **~18k** (16–21k) |

- Kế hoạch ước **~5–6k token** cho phần 3D: thực tế gấp ~3 lần. Phần này nằm trong prefix được cache, nên từ lượt thứ
  hai trong 5 phút trả giá đọc cache (~10%); lượt đầu mỗi phiên trả đủ.
- Nặng nhất: `build_3d_from_plan` 7,3k ký tự, `sweep` 3,6k, `section_view` 3,3k, `loft` 3,2k, `revolve` / `extrude`
  ~3k. Bộ thuộc tính chung (`color`, `linetype`, `lineweight`, `layout`) lặp ở 10 lệnh dựng khối: 8,4k ký tự.
- **Đề xuất giảm (chưa làm, chờ chủ dự án chốt; không cắt lệnh):**
  1. Bỏ `color` / `linetype` / `lineweight` / `layout` khỏi 10 lệnh dựng khối (đổi sau bằng `set_entity_props`; khối 3D
     trên layout gần như không dùng): ≈ −2,4k token ở gói 3D / Full.
  2. Gộp 6 lệnh khối cơ bản (`draw_box` … `draw_pyramid`) thành một lệnh có `kind`: ≈ −2k token. Đổi tên lệnh nên đụng
     macro đã ghi, nhãn gói, test.
  3. Rút mô tả `build_3d_from_plan` / `sweep` / `loft` / `section_view`, chuyển ví dụ và quy trình sang skill
     `ve-3d-kien-truc` (chỉ nạp khi cần): ≈ −2–3k token.
  4. Rút câu mô tả điểm dùng chung (`point`, lặp ở mọi tham số tọa độ): ít lợi hơn, đụng cả gói 2D.

## Điểm cần chủ dự án chốt (chưa sửa)

- **`explode_entities` trên khối 3D** (lệnh nhóm 2D, có ở gói 2D / Full): phá khối có sẵn thành mặt / vùng mà chỉ hỏi
  khi > 20 đối tượng, không theo luật "đổi hình khối có trước luôn qua thẻ". Không thuộc danh sách đã chốt (cộng / trừ,
  cắt, bo, vát, làm rỗng) và sửa thì đổi hành vi một lệnh 2D, nên tớ để nguyên và đưa vào checklist.
- **Skill 2D `ve-kien-truc` vẫn nạp ở gói 3D**: hướng dẫn kích thước / chữ mà gói 3D không có (Claude sẽ báo ngoài gói;
  tốn token skill). Có thể thêm `plan: 2d` cho skill này (cần mở rộng `skillPlan`, việc nhỏ).
- Mô tả `capture_view` ở gói 2D vẫn kể tùy chọn góc nhìn / kiểu hiển thị 3D (lệnh chung, chỉ đọc): vô hại, tốn ít token.

## Còn chỉ thấy trên AutoCAD thật

Như các bàn giao trước: `GenerateSectionGeometry` (L9, L11), cung / spline sau `Explode` (L1, L9, L10), hướng nêm (J10),
dấu góc vát (J11), dấu `ShellBody` (K17), thứ tự ma trận `align` (K21), kiểu hiển thị trên AutoCAD không phải tiếng Anh
(J14). Thêm: lịch sử cuộc chat có lệnh 3D khi máy đổi về gói 2D (T4).

## Việc còn lại trước khi phát hành (không làm, chỉ liệt kê)

1. **Máy chủ kích hoạt riêng:** chủ dự án dựng theo `docs/dung-may-chu-kich-hoat-full.md`, gửi URL + 2 dòng khóa công
   khai; một thread gắn vào `activation/activation.json` và `License.cs` (`PublicModulus`).
2. **Số phiên bản** của bản Full (plugin và trợ lý vẫn ghi 0.9.4 như bản gốc).
3. **Bộ cài thử** build sau bước 1, chủ dự án thử theo `docs/checklist-tong-the.md`.
4. **Secret CI** của repo Full (chủ dự án tự cài); hiện bước phát hành của CI bị chặn.
5. **Merge `main`**: cần chủ dự án đồng ý rõ ràng (merge `main` có phiên bản mới là CI tự phát hành).
6. **Các chỗ trỏ về dự án gốc** trong `docs/cho-tro-ve-repo-goc.md`: nguồn cập nhật / Releases, `AppId` và thư mục bộ
   cài (thay thế hay cài song song bản gốc), số Zalo liên hệ, link tải trong `README.md` / `bo-cai/README.md`.
7. Các đề xuất giảm token và 2 điểm chốt ở trên (tùy chọn).

## Tiến độ

- [x] Bước 0: gộp tài liệu luồng chính (không xung đột)
- [x] Rà soát + sửa lỗi + test
- [x] Tài liệu: file này, `docs/checklist-tong-the.md`, checklist thử thật, kế hoạch, `bo-cai/README.md`
- [ ] CI Windows (1 lượt)
