# Kiểm tra độc lập đợt H phần 2 (H6 → H9), 26/11/2026

Thread `session_01D56HmJCdGyyQVo7VxMC1g4` (agent mới, chưa viết hay rà phần nào của đợt này). Chỉ ghi lỗi, không sửa, không
commit / push / CI. Luồng chính lưu toàn văn báo cáo thread gửi về (26/11 23h29); phần "Lượt sửa" ở cuối do luồng chính ghi.

- Nhánh kiểm: `full/nang-cap-h9-test` @ `adf0093` (code giống `7834e43`, chỉ khác 1 dòng bàn giao). Phạm vi: `76ecfae..HEAD`.
- Đã soát thêm `full/nang-cap-h-tong-the-2` @ `8c46a8e`: nhánh này chỉ đổi code một câu trong mô tả `draw_mep` (TTH2-1). Không
  lỗi nào dưới đây được sửa ở đó. Chỗ tài liệu / checklist của nhánh đó còn lệch được ghi ngay trong lỗi (KTP-16, 30, 36).
- Cách làm: rà sâu H6, H7, H8, H9 bằng 4 tác vụ con song song. Mỗi tác vụ chạy test thử trên bản sao repo trong scratchpad, test
  thử không giữ lại. Thread chính soát lại từng lỗi nặng / vừa trên đúng dòng code.

## 1. Test và build (trên `adf0093`)

| Việc | Kết quả |
| --- | --- |
| `dotnet test plugin/ClaudeCAD.Core.Tests` | **1682 / 1682** qua |
| `cd agent && npm ci && npm test` | **466 / 466**, `# fail 0` |
| Build `net48`, `net8.0-windows`, `net10.0-windows` (xóa `obj` / `bin` giữa các lần, `EnableWindowsTargeting`) | 0 lỗi. Một cảnh báo cũ CA2022 ở `Drawing/TraceTools.cs:505` (đợt G5, ngoài phạm vi) |

## 2. Bảng lỗi

Tổng **36 lỗi**: 3 nặng, 18 vừa, 15 nhẹ. Cột "Thread sửa" do luồng chính ghi: **A** = thread đợt F1
(`full/nang-cap-h-sua-ktp-a`), **B** = thread Test & sửa F1 (`full/nang-cap-h-sua-ktp-b`), **C** = thread đợt F2 (ngay trên
`full/nang-cap-h-tong-the-2`, gộp hai nhánh kia).

### Nặng

| Mã | Mảng | Thread sửa | File : dòng | Lỗi, cách tái hiện | Đúng phải là | Gợi ý sửa |
| --- | --- | --- | --- | --- | --- | --- |
| KTP-01 | H9 | A | `plugin/ClaudeCAD.Plugin/UI/ChatControl.cs:868` | **Chọn English trong ⚙ không có tác dụng.** Ô `_language` không gắn `SelectionChanged`. Các ô khác đều có (dòng 316, 946–948), và grep cả plugin không thấy chỗ nào gắn cho `_language`. Vì vậy `OnSettingsUi` (dòng 1095, đọc `_language` ở dòng 1102) chỉ chạy khi khách đổi một ô khác. Khách chọn English mà khung chat vẫn tiếng Việt: checklist H9 B1 hỏng. Test UI `NangCapH9LanguageUiTests` gọi thẳng `Relabel(...)` nên không bắt được | Đổi ô là khung chat đổi tiếng ngay | `_language.SelectionChanged += (s, e) => OnSettingsUi();` cạnh dòng 948, thêm test UI đổi `SelectedIndex` |
| KTP-02 | chéo (luật xác nhận cũ, H9 mở rộng) | A | `agent/src/agent/confirmation.ts:243` (`NEGATIONS`), `:262` | **"ko đồng ý" được tính là ĐỒNG Ý.** Chạy `isConfirmation` thật: "ko đồng ý", "k đồng ý", "kg đồng ý", "ko tiếp tục", "k tiếp tục", "chẳng đồng ý", "chả đồng ý", "đâu có đồng ý", "có đồng ý đâu", "đồng ý đâu mà", "ai đồng ý" → `true`. Lý do: "k / ko / kg / hông / hong / chẳng / chả / đâu" không có trong `NEGATIONS`, còn cụm "đồng ý" nằm trong 3 chữ đầu. Hậu quả: thẻ hỏi xóa > 20 đối tượng, khách gõ "ko đồng ý" mà lệnh vẫn xóa | `false` | Thêm `k, ko, kg, hông, hong, chẳng, chả, đâu, ai` vào `NEGATIONS`, thêm test các câu trên |
| KTP-03 | H9 | A | `confirmation.ts:243-246` | **Câu có điều kiện vẫn là ĐỒNG Ý.** "đồng ý nếu giữ tường", "yes if you keep the walls", "ok if walls stay", "ok unless doors" → `true`. Bản sửa H9T-2 mới thêm "but / except / nhưng / trừ", chưa có "nếu / if / unless". Bàn giao `test-tong-the-dot-h-phan-2.md` §1 (dòng H9) cũng ghi sai là "có điều kiện = không đồng ý" | `false` (câu không rõ = không đồng ý) | Thêm `nếu, neu, if, unless, miễn, mien` vào `NEGATIONS` |

### Vừa

| Mã | Mảng | Thread sửa | File : dòng | Lỗi, cách tái hiện | Đúng phải là | Gợi ý sửa |
| --- | --- | --- | --- | --- | --- | --- |
| KTP-04 | H9 | A | `confirmation.ts:243`, `:257` | Câu mỉa mai, "thôi", "để sau / chờ", câu hỏi không có "?" vẫn là ĐỒNG Ý: "đồng ý cái gì mà đồng ý", "đồng ý cái con khỉ", "dạ thôi", "ừ thôi", "yes later", "đồng ý sau", "ok hold on", "có, chờ đã", "ok đợi đã", "đồng ý hả", "ok ko", "ok ❌" → `true` | `false` | Thêm `thôi, thoi, later, hold, chờ, đợi, hả, ❌` (và "ko" cuối câu, theo KTP-02). Tránh "sau" đứng một mình vì dễ chặn nhầm "đồng ý, sau đó…" |
| KTP-05 | H9 | A | `plugin/ClaudeCAD.Plugin/PluginApp.cs:40-50` | Máy chọn English mà lúc mở AutoCAD vẫn hiện tiếng Việt: dòng "đã nạp. Gõ CLAUDECHAT…" (dòng 50), bong bóng thanh trạng thái `L.Tr("Mở khung chat")` (`StatusTray.cs:147`). Lý do: `L.Lang` chỉ được đặt trong hàm dựng `ChatController` (`ChatController.cs:141`), mà hàm này chỉ chạy khi mở khung chat | Tiếng Anh ngay từ lúc nạp plugin (checklist H9 A4) | Trong `Initialize`, đặt `L.Lang` theo cài đặt (hoặc `InstallerLanguage()`) trước dòng 50 |
| KTP-06 | H9 | A | `agent/src/index.ts:131`, `:141`; `agent/src/tools/batchFiles.ts:47`; `capture.ts:61`; `render.ts:60`, `:75` | Chữ trợ lý hiện cho khách chưa qua `tr()`, vẫn tiếng Việt khi English: lỗi `turn_end` "Trợ lý không đọc được tin nhắn…"; `info: 'sidecar sẵn sàng'` (plugin hiện "Connected · sidecar sẵn sàng"); tiến độ "file i / n (Dừng để ngừng…)"; "ảnh W×H" (`content.ts` có bản `tr` nhưng `capture.ts` không dùng); "bắt đầu render, khoảng … s"; "AutoCAD đang bận render…". Bàn giao `nang-cap-h9-test.md` §4 ghi "các chỗ hiện cho khách đều đã bọc": sai | Tiếng Anh | Bọc `tr()` các chỗ trên, thêm vào `i18nEn.ts` |
| KTP-07 | H9 (đường phụ) | A | `Sidecar/Activation.cs:283`, `:395`, `:485`; `installer/ClaudeCAD.iss:838`, `:967` | Lời báo lỗi máy chủ kích hoạt là câu tiếng Việt cố định trong `Code.gs.template`, plugin và bộ cài ghép nguyên văn. Khách English thấy câu lẫn hai thứ tiếng, vd "Could not send the request: Máy chủ kích hoạt đang bận…" | Toàn tiếng Anh | Thêm các câu cố định của máy chủ vào `LangEn.cs` và `.iss`, gọi `L.Tr(r.error)`; hoặc máy chủ trả mã lỗi (không đụng máy chủ nếu chủ dự án chưa chốt) |
| KTP-08 | H6 | C | `Core/Terrain.cs:381` | File không tiêu đề, 4 cột "tên số, X, Y, Z", tên điểm nhảy bước (1, 4, 9, 15): `LooksLikeRowNumbers` sai nên cột 1 bị coi là X. Test thử: X = 1, Y = 1234012,5 (cột Bắc), Z = 567045,25; cột cao độ thật 12,35 bị bỏ. Điểm vẽ ở cao độ khoảng 567 km, không cảnh báo | tên = A, X = B, Y = C, Z = D | Cột 1 là số nhỏ (< `RealCoordinateM`) trong khi cột 2, 3 lớn hơn nhiều thì coi cột 1 là tên |
| KTP-09 | H6 | C | `Terrain.cs:377` | File 3 cột "tên số, X, Y" (không cao độ): nhánh `n == 3 && firstNumber` cho X = tên, Y = X, Z = Y. Test thử: X = 1, Y = 1234012,5, Z = 567045,25 | tên, X, Y, không Z | Dùng cùng phép thử của KTP-08 trước nhánh `n == 3` |
| KTP-10 | H6 | C | `Terrain.cs:206-209`, `:307` | Số có dấu nghìn kiểu Việt `1.234.012,500` (Excel tiếng Việt lưu CSV `;` theo chữ đang hiển thị), hay dấu nghìn U+00A0: 0 điểm, mọi dòng báo "X / Y is not a number". Khách đưa thêm cột cũng không cứu được | X = 1234012,5 | Khi tách cột không phải phẩy: bỏ `.` / khoảng trắng / U+00A0 giữa các nhóm 3 chữ số trước phẩy thập phân |
| KTP-11 | H6 | C | `Terrain.cs:258-263` | `.xlsx` có ô số lưu dạng chữ "1234012,5" (chép từ phần mềm đo): `ReadXlsx` không đổi phẩy thập phân, ra 0 điểm, "row 2: X / Y is not a number" | Đọc được 1 điểm | Cho kết quả `ReadXlsx` đi qua cùng bước đổi phẩy của `ParseText` |
| KTP-12 | H6 | C | `Terrain.cs:183-193` | File .txt cách nhau bằng dấu cách, số dùng phẩy thập phân (`M1 1234012,5 567045,25 12,35`): `Delimiter` chọn `,`. Ra tên "M1 1234012", X = 5567045, Y = 2512, Z = 35, vẽ ra luôn mà không báo | X = 1234012,5… | Không có tab / `;` mà ô tách bằng `,` lại chứa dấu cách nối hai số thì tách bằng dấu cách rồi mới đổi phẩy |
| KTP-13 | H7 | B | `Drawing/MepTools.cs:399-400` (`Reach`), `:355-358` | Ống gió plugin tự đặt bằng `under_beams` (đỉnh cách đáy dầm 100) bị `mep_report clash` báo "sát" chính dầm đó, vì bán kính ống gió lấy nửa đường chéo tiết diện. Test thử `PipeBoxGap`: 600×300 và 400×300 ra khe hở 0, "close"; 300×200 ra 19,7, "close". Ống tròn D32 đúng | Khe hở 100, không báo | Ống gió đo theo nửa rộng / nửa cao trên đúng trục tiết diện (hoặc hộp với hộp khi thẳng trục) |
| KTP-14 | H7 | B | `MepTools.cs:346-347`, `:357`, `:364` | Đổi đường kính bằng hai tuyến nối đầu nhau (mỗi lần gọi chỉ một cỡ), hoặc một tuyến đi tiếp qua nhiều lần gọi: bị báo "sát 0 mm". Test thử: `PipePipeGap((0,0,2800)→(4000,0,2800), 55, (4000,0,2800)→(7000,0,2800), 45)` ra 0, "close". `CheckInterference` lúc hai mặt chỉ chạm nhau cho thể tích giao 0, nên dòng "sát" vẫn ở lại | Không báo | Bỏ qua cặp có đầu trục trùng nhau, hoặc thêm `continue_from` ghi `Link` |
| KTP-15 | chéo (macro × H7) | B | `Core/Macros.cs:171-173` (`IsHandleKey`) | `connect_to` của `draw_mep` không được coi là handle: `IsHandleKey("connect_to")` ra `false`, `Remap` giữ handle cũ, `HandleParams` rỗng. Chạy macro "ống chính + nhánh có tê" thì nhánh nối vào ống chính cũ: ống cũ đã xóa thì báo lỗi; ở bản vẽ khác thì handle trỏ vào đối tượng khác của khách. Bước chặn `Unresolved` cũng bỏ qua | Handle được đổi theo lượt phát lại | Thêm `k == "connect_to"` vào `IsHandleKey` |
| KTP-16 | checklist + H7 | B (+ C chép checklist tổng) | `docs/checklist-dot-h7.md` A3; `checklist-tong-the.md` P2-H7 mục 3 (dòng ~1242 trên tong-the-2); `MepTools.cs:124-136` | Làm đúng như checklist thì không ra tê. Ống thoát A2 cao độ 2800 ở (0,0), dốc 2 %, nên tại x = 1000 tâm ống ở z 2780; nhánh cho cao độ 3000, lệch 220 > bán kính 55, nên lệnh báo "not on pipe". A3 còn nối ống **cấp** vào ống **thoát**. Code không chặn nối khác hệ: tê nằm layer `P-SUPL-3D` nhưng theo cỡ ống thoát | Checklist ra tê; nối khác hệ thì báo | Viết lại A3: nhánh thoát D60, cao độ bằng tâm ống chính tại điểm nối; code kiểm `main.Mark.System == sys.Key` |
| KTP-17 | H7 | B | `Core/Mep.cs:154-155` (`BendRadius`), `:178-260` (`Layout`) | Không kiểm bán kính co so với nửa tiết diện trong mặt phẳng uốn. Ống gió 200×500 rẽ đứng: bán kính co = bề rộng 200, bán kính trong −50, khối xoay tự cắt. `bend_radius: 20` với D110 cũng được nhận. Nhiều khả năng `Revolve` báo lỗi AutoCAD thô (chưa chạy AutoCAD thật; hình học đã chứng minh bằng test thử) | Báo rõ, hoặc lấy bán kính theo cạnh trong mặt phẳng uốn | Trong `Layout` từ chối khi bán kính ≤ nửa tiết diện trong mặt phẳng uốn; ống gió rẽ đứng lấy theo chiều cao |
| KTP-18 | H8 | B | `Drawing/StructureTools.cs:83-87` | `build_footing` móng băng `rib: false` (mô tả lệnh hướng dẫn "false = none") luôn lỗi: dòng 83 gọi `p.NumbersOpt("rib")`, hàm này ném "parameter 'rib' must be an array of numbers (got false)". Dòng 87 không bao giờ chạy tới | Móng băng không sườn | `var rib = p.Get("rib") is bool ? null : p.NumbersOpt("rib");` |
| KTP-19 | H8 | B | `Core/Rebar.cs:166-178` (`StirrupPositions`) | Đai dầm đặt từ trái sang nên lệch: 1/4 nhịp phải ít hơn trái một đai; đai cuối có thể cách gối phải 190 mm (> a150). Nhịp 3000: 5 / 4; 4000: 7 / 6; 5040: 9 / 8, đai cuối cách 190. Tổng số đai trong bảng thép lệch theo. Chú thích dòng 164 ghi "half a spacing" trong khi code dùng 50 mm | Đối xứng, mỗi đầu đai đầu cách gối 50 | Tính nửa trái rồi lấy đối xứng (`span − x`), đoạn giữa chia đều a200 |
| KTP-20 | H8 | B | `Core/Rebar.cs:186-187` (`BarPositions`) | Kiểm khe hở thép dầm so tâm–tâm với d (`gap < barDia`), trong khi lời báo nói "less than one bar between them". 5Ø20 trong dầm rộng 200 được nhận, khe hở thật 9,5 mm; 4Ø20 rộng 200: 19,3 mm (< d). Hàm thép cột `ColumnBars` (dòng 205) đã kiểm đúng | Báo không đủ chỗ | `if (gap - barDia < barDia - 1e-9) throw …` |
| KTP-21 | H8 | B | `Core/Rebar.cs:142-143` (`MainHook`) | Móc 180° thép trơn cộng π(2,5d + d)/2 + 3d = 8,5d lên chiều dài đã đo tới mép lớp bảo vệ, không trừ đoạn đã nằm sẵn trong chiều dài thẳng. Quy ước thường dùng là +6,25d mỗi móc. Sàn 3000 × 4000 Ø8a200: L = 3096, tính tay 3060 (+36 mm mỗi thanh). Số duyệt (2,5d, 3d) không đổi, chỉ cách cộng. Nếu chủ dự án coi đây là quy ước đo thì hạ xuống nghi vấn | 6,25d mỗi móc | `MainHook = π(2,5d+d)/2 + 3d − (1,25d + d)`; kỹ sư chốt |

### Nhẹ

| Mã | Mảng | Thread sửa | File : dòng | Lỗi | Gợi ý sửa |
| --- | --- | --- | --- | --- | --- |
| KTP-22 | H6 | C | `Terrain.cs:322`, `:344` | Tiêu đề "STT \| Tên điểm \| X \| Y \| Z": tên lấy theo STT ("1", "2" thay vì "GPS01") vì "stt" nằm trong `NameKeys` và khớp trước | Tìm hai lượt: ưu tiên "tên / tên điểm / số hiệu / mã điểm" trước "stt / tt / id" |
| KTP-23 | H6 | C | `Terrain.cs:197`, `:247-265`, `:395` | Số dòng trong `skipped_rows` không khớp file: bỏ dòng trống trước khi đánh số; .xlsx bỏ thuộc tính `r` của hàng. Dòng hỏng ở dòng 4 của file bị báo "row 2" | Giữ số dòng gốc đi kèm từng hàng |
| KTP-24 | H6 / tài liệu | C | `TerrainTools.cs:116`; `agent/src/tools/terrain.ts:15`, `:40`; `skills/ve-ha-tang/SKILL.md:71` | Kết quả và mô tả lệnh vẫn ghi "metres × 1000", `position` "WCS, mm", dù H6T-3 đã đổi theo đơn vị bản vẽ. Bản vẽ đơn vị mét có hệ số 1: Claude báo sai cho khách, và có thể đưa `position` theo mm làm khung trắc đặt xa 1000 lần | Ghi `UnitsPerMetre` thật vào `axes`; mô tả: "m × đơn vị bản vẽ", `position` "đơn vị bản vẽ" |
| KTP-25 | H6 | C | `Terrain.cs:1006-1019` (`Breakpoints`) | Trắc ngang: ngưỡng phẳng 1 cm (`CrossFlatM`, số trong `dot-h-cho-duyet.md`) bị vượt, sai số cộng dồn. Mặt đất z = 0,002·off², mẫu 1 m, ±15 m: giữ 7 / 31 điểm, sai số lớn nhất 0,018 m, lộ ra vì trắc ghi 2 số lẻ | Kiểm mọi mẫu giữa điểm đã giữ và mẫu kế (Douglas–Peucker một chiều) |
| KTP-26 | H6 | C | `Terrain.cs:805`, `:824` | Đỉnh tuyến trùng cọc cuối (trong 1 cm): kind thành "end+vertex2", cọc cuối bị gọi "C43/Đ2" thay vì "Cuối/Đ2". Cọc đầu cũng vậy. Ít gặp vì `GetAlignment` bỏ đỉnh cuối | So `kind.StartsWith("end")` / `StartsWith("start")` |
| KTP-27 | H7 / Excel | B | `Core/Xlsx.cs:24-41` | `mep_report` không có trong `DefaultSuffix` / `SheetName`: file tên `nha-bang.xlsx`, sheet tên "mep_report"; thống kê và danh sách va chạm ghi chung một tên file | Thêm `["mep_report"] = "ong-mep"`, sheet "Thống kê ống MEP"; tách hậu tố cho clash |
| KTP-28 | H7 / Excel | B | `MepTools.cs:370`, `:386` | Excel danh sách va chạm ghi mã hệ tiếng Anh (`supply`, `duct_supply`), còn Excel thống kê ghi tên tiếng Việt | Bảng Excel dùng `MepLogic.Systems…Name`; JSON cho Claude giữ mã |
| KTP-29 | H7 | B | `MepTools.cs:83`, `:92-101` | Tuyến trộn điểm 2D và 3D: z của điểm 3D bị thay bằng `elevation`, không báo. Mọi điểm có z mà vẫn cho `elevation`: `elevation` bị bỏ qua, không báo (trong khi cho `slope` thì báo lỗi) | Báo lỗi / cảnh báo hai trường hợp này |
| KTP-30 | tài liệu / checklist H7 | B (+ C chép checklist tổng) | `docs/ban-giao/nang-cap-h7.md` mục 1; `checklist-dot-h7.md` C5 = `checklist-tong-the.md` P2-H7 C mục 5 (dòng ~1264 trên tong-the-2); mục D | Bàn giao còn ghi "sát … đo giữa hai hộp bao" (code đo từ trục ống). C5 ghi "đoạn dốc nhất mức 1 %": đúng là đoạn **phẳng nhất**. Mục D đánh số 1, 2, 3, 4, 6, 5 | Sửa chữ |
| KTP-31 | H8 | B | `StructureTools.cs:273-282` | Nhịp dầm < 100 mm: `StirrupPositions` rỗng, `Record(ObjectId.Null, …)` ném lỗi AutoCAD thô | `if (pos.Count == 0) throw ToolException.Invalid(…)` |
| KTP-32 | H9 | A | `agent/src/tools/interaction.ts:56`, `:94` | Mô tả `propose_plan` ("short Vietnamese steps") và `suggest_next_steps` ("(Vietnamese, imperative…)") còn sót chữ "Vietnamese", mâu thuẫn với English. `ENGLISH_SECTION` có dặn bỏ qua. Bàn giao `nang-cap-h9-test.md` §3.1 ghi "không còn câu dặn nào": sai | "in the user's language" |
| KTP-33 | H9 | A | `Core/LangEn.cs` | 21 khóa không còn dùng (mảnh câu cũ trước khi chuyển sang `{0}`: "Chưa đăng nhập xong (", "). Bấm lại để thử lần nữa.", "Đã lưu gói ", " cho máy này."…). Không có test bắt khóa thừa | Xóa, thêm test "mọi khóa đều có trong code" |
| KTP-34 | H9 | A | `Core/LangEn.cs` | Ba bản tiếng Anh dùng chung cho hai câu Việt: " — stopped midway" (đã dừng giữa chừng / dừng giữa chừng), "Plan: " (**Kế hoạch:** / **Gói:**), "Added" (Đã thêm / Thêm). `Retranslate` về tiếng Việt chỉ chọn được một câu. Riêng "Plan" vừa là kế hoạch vừa là gói, dễ hiểu lầm | "Gói: " → "Service plan: " |
| KTP-35 | H9 / chéo | A | `Core/Plans.cs` (`ModeBlockedMessage`, `NoTwoDReason`, `BlockedParamMessage`) | Câu mẫu dặn Claude nói với khách: "Đang ở chế độ {label}, đổi sang {other} hoặc Cả hai (nút chế độ…)", trong đó `{label}` đã qua `L.Tr` (English: "Both"), còn "Cả hai" thì cố định. Khi English, câu gửi Claude lẫn "Both" với "Cả hai"; nút trên khung chat ghi tiếng Anh. Claude thường tự dịch, nhưng tên nút có thể lệch | Để `{label}` không dịch trong câu gửi Claude, hoặc thêm tên nút English vào câu |
| KTP-36 | tài liệu | C | `docs/ban-giao/test-tong-the-dot-h-phan-2.md` §1 dòng H9 (tong-the-2) | Ghi "xác nhận gõ tay có điều kiện … = không đồng ý": chưa đúng (KTP-03) | Sửa sau khi vá KTP-02 → 04 |

## 3. Bảng xác nhận gõ tay (`isConfirmation` thật, khoảng 230 câu)

- **Đúng, ra đồng ý:**
  - đồng ý / Đồng ý / ĐỒNG Ý / dong y, có, "Có, xóa đi";
  - ok / OK / oke, y, yes / "YES!", yes please, go ahead, sure, proceed, approved, agreed, confirm, delete it, do it, sounds
    good, that is fine;
  - có dấu nháy cong: “ok”, ‘yes’;
  - "ok không sao", "không sao, xóa đi", ừ, vâng, dạ, tiếp tục đi.
- **Đúng, ra không:**
  - không đồng ý, khong dong y;
  - "yes but no", "not yes", "I don't agree", "I don’t agree", disagree, nope, cancel, never mind;
  - "ok trừ cửa", "ok but keep walls", "yes, except walls", "ok, nhưng giữ lại";
  - "ok, stop", "yes, wait", "đừng xóa";
  - "ok?", "đồng ý không", "được không", "có thể", maybe, chờ đã;
  - k, ko, n, u, co, 👍, ✅, ❌.
- **Sai, ra đồng ý:** xem KTP-02, 03, 04.
- **Ra không, an toàn (khách phải gõ lại):** "I agree", agree, yeah, yup, of course, fine, "ok👍".

## 4. Đã kiểm, không thấy lỗi

**H6**
- Mã hóa UTF-8 có / không BOM, 1258, 1252.
- Phẩy thập phân khi tách bằng `;` / tab; tiêu đề có "(m)", "Toạ độ X"; 4 cột X, Y, Z, mã (H6T-2).
- Đổi trục VN-2000 đúng chiều. `AutoOrigin`. Tọa độ thật thì hỏi gốc.
- `ForDrawing` trả `UnitsPerMetre` về 1000 cả khi lỗi giữa chừng (`using`).
- Lưới tam giác:
  - nội suy tính tay 3 điểm đúng;
  - lưới đều, các điểm cùng nằm trên một đường tròn: phủ đủ;
  - 10 000 điểm dựng lưới trong 85–320 ms.
- Đồng mức: đường cái ở bội 5 × khoảng cao đều, kể cả mức âm.
- Lý trình:
  - `Km0+020.00`; 999,999 m ra `Km1+000.00`;
  - cọc Đầu / Km / H / C / Đ / Cuối;
  - vạch 50 m mặt bằng chỉ ghi lý trình.
- MSS chẵn mét, thấp hơn ≥ 2 m (tính cả đường thiết kế). Tỉ lệ 1:1000 / 1:100, trắc ngang 1:200, rộng ±15 m, trái / phải đúng.
- Gói và chặn:
  - `terrain` thuộc nhóm 2D;
  - dispatcher kiểm gói cả trong batch; "Chỉ xem" chặn;
  - giữ WCS; `batch_files` không gọi `terrain`.
- Tên / mã điểm từ file chỉ vẽ ra bản vẽ, không quay lại kết quả cho Claude. `terrain` không xuất Excel.
- H9 không sửa code H6; chữ vẽ giữ tiếng Việt.

**H7**
- Dốc mặc định: D60 / D90 2,5 %, D110 2 %. Checklist A2 tính tay: 7000 × 2 % = 140. `SlopesOf` bỏ đoạn đứng, báo dốc ngược.
- Layer / màu: P-SUPL 5, P-HOT 1, P-DRAIN 3, P-VENT 8, M-DUCT 4, M-DUCT-RET 6, F-SPRK 1, hậu tố `-3D`. `check_drawing` nhận
  `P-` / `M-` / `F-` / `S-`.
- Gói:
  - `draw_mep` / `mep_report` thuộc nhóm 3D, hoãn nạp;
  - `mep_report` có thẻ ghi đè Excel.
- Đơn vị mm / cm / m nhất quán. `elevation` cộng gốc z của UCS; UCS nghiêng bị từ chối.
- H9 không sửa code H7.

**H8**
- Neo 30d; thép gân bẻ L 10d (2Ø18 nhịp 5000: 6440; 2Ø16: 6280). Nối 35d cột (4Ø16 cao 3300: 3860), 40d dầm / sàn.
- Cảnh báo dưới mức tối thiểu đúng (Ø18 neo 250 báo mức 270…).
- Đai:
  - móc 135° max(6d, 75), dầm 220 × 400 Ø6: 1166;
  - đai cột floor((L − 100) / a) + 1.
- Thanh > 11,7 m:
  - dầm 11 m 3Ø20: 12600 → 1 mối, 13400;
  - cột Ø25 dài 12000: 12880.
- Làm tròn 10 mm. Khối lượng 0,00617·d², 2 số lẻ (Ø25 51,52 m → 198,67 kg). Ø ≤ 10 CB240-T, Ø ≥ 12 CB400-V.
- Bảng thống kê:
  - dòng "Cộng Ø…" và Tổng khớp; không có cột hao hụt mặc định;
  - số hiệu trùng: khác loại thì tách dòng và báo; gọi lần hai lấy số tiếp theo.
- Lớp bảo vệ sàn 20, dầm / cột 25, không nhỏ hơn d. Cột 8Ø20 300 × 300: 4 góc + 4 giữa cạnh; khe hở cột kiểm đúng.
- Tỉ lệ mặt cắt ghi đúng tỉ lệ vẽ. Bê tông lót rộng hơn móng 100 mỗi bên.
- Gói: `build_footing` nhóm 3D; `draw_rebar` / `rebar_schedule` nhóm 2D.
- H9 không sửa code H8; mã "cột" trong `WithLaps` nguyên.

**H9**
- Chỗ trống `{0}`:
  - 940 cặp `LangEn.cs` và 28 cặp `i18nEn.ts` khớp số, thứ tự, định dạng (`{0:N0}`) và khoảng trắng hai đầu;
  - không khóa trùng; không còn chữ Việt trong bản Anh;
  - mọi `Tr("…", args)` đúng số tham số; không có `Tr($"…")` hay `Tr("…" + …)`;
  - mọi khóa `i18nEn.ts` đều dùng.
- Chữ làm mã so sánh không bị dịch:
  - "Kích hoạt" / "Tài khoản Claude" tìm theo tên gốc;
  - ô chọn đọc theo vị trí;
  - code vẽ / Excel H6 / H7 / H8 / G1 / G3 / H3 / H5 không gọi `Tr` (có test);
  - `ManualChangeReport.Build` chỉ ghép câu gửi Claude;
  - nút thẻ gửi cờ `confirm` / `plan`, không so chữ.
- Đổi ngôn ngữ giữa chừng:
  - ngôn ngữ nằm trong `planKey`, trợ lý khởi động lại và tiếp tục cùng cuộc chat;
  - prewarm không đóng lượt đang chạy;
  - tên nút trong `ENGLISH_SECTION` khớp nút English (Yes, No, Do it, Edit plan, Skip, View only).
- Bộ cài (đọc `.iss`):
  - Tiếng Việt đứng đầu, chọn sẵn (`LanguageDetectionMethod=none`);
  - `language.txt` chỉ dùng khi cài đặt chưa có mục ngôn ngữ, nên cài lại không đổi ngôn ngữ;
  - dòng liên hệ English giống nhau giữa `LangEn.cs:894` và `.iss` (`ActivationContactEn`).

**Chéo**
- Sáu lệnh mới đúng nhóm gói.
- Thêm lệnh không làm hỏng test cũ: 1682 lõi, 466 trợ lý.
- tong-the-2: `dot-h-cho-duyet.md` phần 2 các số H6 đã soát (10 000 điểm, 20 MB, 1 000 cọc, 200 trắc ngang, 2 000 mức, 1:500,
  1 m / 0,01 m, 4 × trung vị, trùng < 1 mm, > 100 000 m, layer / màu) khớp tên hằng. Riêng 0,01 m bị vượt khi cộng dồn (KTP-25).

## 5. Không tính là lỗi (quyết định đã ghi)

- Skill `ve-mep` / `ve-ket-cau` / `ve-ha-tang` là `plan: 2d`: máy gói 3D không nạp phần ống 3D / móng (quyết định 12/11/2026,
  tong-the-2 giữ, đã thêm câu TCVN vào mô tả `draw_mep`). Tác vụ con H7 nêu đây là lỗi vừa; ghi lại để chủ dự án biết: khách gói
  3D không có hướng dẫn skill cho ống 3D / móng.
- `.xlsx` đọc `sheet1.xml`, không theo thứ tự tab (H6-test đã ghi chấp nhận).
- Layer `P-SUPL-3D` bị báo khác chuẩn khi bộ chuẩn công ty chỉ có `P-SUPL` (tong-the-2 ghi quyết định: chuẩn công ty là danh
  sách đóng).

## 6. Nghi vấn chưa chứng minh

- **H6:**
  - Delaunay hụt rìa: 2 000 điểm ngẫu nhiên tọa độ 10⁹ ra 3 973 / 3 977 tam giác (siêu tam giác 20 × span), chỉ thấy khi
    `max_edge: 0`;
  - trắc ngang ở cọc đỉnh lấy hướng đoạn sau, không lấy phân giác;
  - lọc `stakes` "Đ1" không khớp "C3/Đ1";
  - `H6Numbers` là static đổi được, không khóa luồng;
  - `PlaceBelow` dùng `Extmin` / `Extmax`, có thể đã cũ.
- **H7:**
  - `IsStructureLayer` nhận quá rộng (`A-GLASS-3D`, `E-DEN-COT`);
  - hộp bao dầm chéo quá rộng, có thể báo "sát" nhầm;
  - `under_beams` lấy hộp bao cả tuyến chữ L;
  - tê với nhánh gần dọc trục ống chính;
  - hai nhánh vào cùng một điểm của ống chính.
- **H8:**
  - bảng thép không có dòng tổng riêng CB240-T / CB400-V (bảng duyệt ghi "tổng theo đường kính và loại thép");
  - đai móc 135° chỉ cộng 2 đoạn thẳng, không cộng phần cong;
  - `xy` mô tả "mm" nhưng code dùng đơn vị bản vẽ (quy ước chung của dự án).
- **H9:**
  - lời chặn lệnh dặn Claude hỏi đúng câu "Cậu có đồng ý không?" khi English (`ENGLISH_SECTION` có dặn dịch);
  - `Retranslate` thay mục ô chọn khi đang tắt sự kiện: chưa thử WPF thật.

## 7. Chưa kiểm được

- AutoCAD thật:
  - vẽ, UCS xoay / dời gốc, U hoàn tác, INSUNITS mét / cm;
  - `Revolve` co ống gió (KTP-17), `CheckInterference`;
  - cổ móng hợp khối, móng băng nhiều đoạn;
  - vạch lý trình trên cung / polyline 3D.
- Giao diện WPF thật: KTP-01, KTP-05, bong bóng thanh trạng thái.
- Excel thật: file CSV `;` có dấu nghìn do Excel tiếng Việt lưu, mở file `.xlsx` xuất ra.
- Claude thật: trả lời tiếng Anh, dịch câu mẫu, nhắc tên nút.
- Bộ cài chạy thật: chọn ngôn ngữ, cài đè, `/VERYSILENT /LANG=english`.
- Đường lách qua `batch_files` / macro chạy thật (chỉ đọc code bộ điều phối).

## 8. Thời gian và chi phí

Khoảng 55 phút. Chi phí thread tới lúc viết báo cáo khoảng $15 (`get_session`, chưa tính phần gửi báo cáo). Tốn nhất là 4 tác vụ
con rà song song, mỗi tác vụ build và chạy test thử trên bản sao.

## Lượt sửa (luồng chính ghi)

- 26/11 23h25 → 23h32: chuyển lỗi cho ba thread.
  - **A**, thread đợt F1, nhánh `full/nang-cap-h-sua-ktp-a`: KTP-01 → 07, 32 → 35.
  - **B**, thread Test & sửa F1, nhánh `full/nang-cap-h-sua-ktp-b`: KTP-13 → 21, 27 → 31.
  - **C**, thread đợt F2: KTP-08 → 12, 22 → 26, 36 ngay trên `full/nang-cap-h-tong-the-2` (@ `6bcd5b5`), gộp hai nhánh kia,
    chép checklist, CI.
- **Quyết định luồng chính tự chọn** (rẻ, an toàn, đổi lại được; đưa chủ dự án xem ở báo cáo cuối):
  - **Xác nhận gõ tay (KTP-02 → 04):** đổi sang danh sách đóng. Chỉ đồng ý khi cả câu là một lời đồng ý ngắn, rõ ràng; còn lại
    là không đồng ý.
  - **Móc 180° thép trơn (KTP-21):** theo quy ước thường dùng +6,25d mỗi móc. Giữ nguyên số đã duyệt (uốn 2,5d, đoạn thẳng 3d).
  - **KTP-07:** chỉ thêm bản dịch câu báo lỗi của máy chủ kích hoạt trong plugin / bộ cài, không sửa máy chủ.

## Soát lại lượt 1 (27/11 00h19, trên `5611f3f`, code `0d5da4c`)

Thread kiểm tra độc lập soát lại, so với `8c46a8e`. Cách soát: 3 tác vụ con đọc diff và viết test thử trên bản chép. H9 chạy
`isConfirmation` thật với khoảng 230 câu; H7 / H8 có 8 test thử, so khe hở với cách đo vét điểm; H6 có 26 test thử.

Kết quả test: lõi 1708 / 1708, trợ lý 467 / 467, build 3 bản 0 lỗi. Test UI WPF (KTP-01) dựa vào CI 45863381169 xanh.

**Tổng: 31 sửa đúng, 5 chưa đủ (KTP-04, 07, 12, 24, 31), 0 sai, 12 lỗi mới (KTP-37 → 48).**

### KTP chưa đủ

| KTP | Còn thiếu |
| --- | --- |
| 04 (nhẹ) | Câu hỏi bằng "à" không có "?" vẫn ra đồng ý: "ok à", "đồng ý à", "đồng ý rồi à", "ok xóa hết rồi à". Lý do: `'à'` nằm trong `FILLERS` (`confirmation.ts:254`) |
| 07 | Thiếu câu `LOCKED_MSG` ("Máy này đã bị chủ plugin tạm khóa."), xem KTP-41 |
| 12 | File có một dòng chỉ một chữ (vd "SODIEM") vẫn chọn nhầm `,`, xem KTP-39 |
| 24 (nhẹ) | `skills/ve-ha-tang/SKILL.md:11` và `:18` còn ghi "nhân 1000" / "x_cad = Y × 1000". Test chỉ tìm "m × 1000" nên không bắt |
| 31 | Nhịp 101–199 mm vẫn lỗi thô, xem KTP-40 |

### Các KTP sửa đúng (điểm đáng ghi)

- **KTP-13:** 83 ca ngẫu nhiên so với đo vét điểm, không ca nào báo khe lớn hơn thực. Ống gió đứng vẫn tính nửa đường chéo:
  báo khe nhỏ hơn thực, an toàn.
- **KTP-17:** khi đã nâng bán kính mặc định mà đoạn quá ngắn, lời báo vẫn bảo "use a smaller bend_radius".
- **KTP-29:** `under_beams: false` kèm điểm 3D cũng bị từ chối, vì code chỉ kiểm có tham số, không kiểm giá trị.
- **KTP-20:** thread chọn khe thông thủy ≥ d, không có ngưỡng 25 mm. Đây là số tiêu chuẩn, chờ chủ dự án chốt.
- **KTP-35:** "Cả hai" còn ở `Plans.cs:397` và `DrawingSet.cs:101, 118`; dựa vào bảng đổi tên nút, chấp nhận được.
- **KTP-19:** đai dầm a150 / a200 (trái / phải, bước giữa):

  | Nhịp | Đai trái / phải | Bước giữa |
  | --- | --- | --- |
  | 3000 | 5 / 5 | 188,9 |
  | 4000 | 7 / 7 | 190,9 |
  | 7000 | 12 / 12 | 200 |

- **KTP-21:** sàn 3000 × 4000 → 3060 / 4060; dầm 2Ø10 nhịp 5000 → 5725; cột cao 3300 → 3775.
- **Lỗi nặng C tự tìm (`Alignment.At`):** có thật, sửa đúng; thiếu test thật (KTP-47b). Nghi vấn còn lại: cọc cuối có thể vượt
  chiều dài thật 1 ulp (khoảng 1 % số tuyến). Gợi ý kẹp theo `GetDistanceAtParameter(EndParam)`.

### Lỗi mới

| Mã | Mức | Mảng | Chỗ | Lỗi | Gợi ý |
| --- | --- | --- | --- | --- | --- |
| KTP-37 | Vừa | H6 (do sửa 23) | `Terrain.cs:371` `FindHeader` | Chỉ dò tiêu đề trong 10 hàng đầu, mà nay hàng trống được giữ nên chiếm chỗ. Tên bảng ở hàng 1, tiêu đề ở hàng 12 → lấy cột theo vị trí, có thể sai cột | Đếm 10 hàng không rỗng |
| KTP-38 | Vừa | H7 (do sửa 14) | `MepTools.cs:385` | Cặp ống chung một đầu bị bỏ qua trước khi kiểm khối chạm. Ống vẽ trùng hai lần hoặc chồng một phần không còn bị báo | `if (!hit && EndToEnd) continue` |
| KTP-39 | Nhẹ–vừa | H6 (12 còn hở) | `Terrain.cs` `CommasAreDecimal` | Một dòng < 2 chữ trong 20 dòng đầu là trả false, nên X = 5567045 sai mà không cảnh báo | Bỏ qua dòng một chữ |
| KTP-40 | Nhẹ | H8 (31 còn hở) | `Rebar.cs:179-180` | Nhịp 101–199: `left` rỗng, ném `ArgumentOutOfRangeException` | Khi `left` rỗng thêm đai ở `first` |
| KTP-41 | Nhẹ–vừa | H9 (07 chưa đủ) | `Lang.cs` `ServerTexts`, `ClaudeCAD.iss` `TrServer` | Thiếu `LOCKED_MSG`; test chỉ kiểm một chiều | Thêm câu; test đọc mọi chuỗi lỗi trong template |
| KTP-42 | Nhẹ | H9 | `PluginApp.cs:44` | Thiếu `\|\| !settings.LanguageSaved` như `ChatController.cs:140`. Máy có cài đặt cũ cài bản mới chọn English thì dòng "đã nạp" và bong bóng vẫn tiếng Việt | Thêm điều kiện |
| KTP-43 | Nhẹ | H9 | `LangEn.cs:568` | "Đã thêm 12/11/2026" dịch thành "Already added" | "Added on" |
| KTP-44 | Nhẹ (an toàn) | H9 (do sửa 02) | `confirmation.ts:234, 254-255` | "xoá" (dấu kiểu cũ) không có trong danh sách: "ok xoá đi", "đồng ý xoá" ra không đồng ý. Cũng ra không đồng ý: "được thôi", "ok thôi", "ok bạn ơi", "đồng ý thay thế", "ok ghi đè", "yes overwrite", "yeah", "fine", "alright", "👍" | Thêm `xoá`, `xoa`; cân nhắc "ghi đè", "thay thế", "overwrite", "ơi" |
| KTP-45 | Nhẹ–vừa | H7 (nghi vấn B) | `MepTools.cs` `PipeBoxGap` | Dầm chéo có hộp bao mặt bằng rộng; ống đứng qua sàn trong hộp đó bị báo "sát 0 mm" dù không chạm. `under_beams` với dầm chéo có thể chọn nhầm dầm | Đo theo khối dầm thật, hoặc hộp chữ nhật theo trục dầm |
| KTP-46 | Nhẹ | H7 (nghi vấn B) | `MepTools.cs:376` | Hai nhánh vào cùng một điểm ống chính: hai tê trùng nhau, `clash` báo "chạm" giữa hai tê | Bỏ qua cặp tê cùng ống chính, hoặc ghi vào "Giới hạn" |
| KTP-47 | Nhẹ | Tài liệu | `dot-h-cho-duyet.md`, bàn giao A / C, checklist P2-H6, `checklist-dot-h6.md` | (a) Tài liệu ghi "thôi được", "không sao" là đồng ý, nhưng gõ riêng thì ra không đồng ý. (b) Bàn giao C ghi "SelfTest sẽ báo" lỗi `Alignment.At`: sai. (c) Checklist không có mục thử tay cho bản sửa H6. (d) Thiếu quyết định tự chọn KTP-12, 22, 26 | Sửa chữ; thêm SelfTest "cọc cuối cách điểm cuối polyline < 1 mm"; thêm mục checklist |
| KTP-48 | Nhẹ (có từ trước) | H6 | `Terrain.cs` đọc `.txt` | Tiêu đề "Tên điểm  X  Y  Z": tên 2 chữ làm lệch cột, ra 0 điểm | Ghép tiêu đề theo từ khóa, hoặc tách bằng ≥ 2 khoảng trắng |

### Ba nghi vấn B để lại

1. Hộp bao dầm chéo: có báo sai, ghi thành KTP-45.
2. Tê:
   - nhánh gần dọc trục ống chính: chỉ là chuyện hình vẽ, không ghi lỗi;
   - hai nhánh vào cùng một điểm: ghi thành KTP-46.
3. Móc đai 135°: không phải lỗi rõ. Code không cộng phần cong nhưng góc vuông lại tính dư, hai phần gần bù nhau, lệch khoảng
   ±2–3 %. Đổi cách tính thì kỹ sư / chủ dự án chốt.

### Cần chủ dự án chốt

- **KTP-20:** khe hở giữa hai thanh thép dầm. Hiện chỉ đòi ≥ d; TCVN 5574 thường đòi max(d, 25 mm) với thép dưới, max(d, 30 mm)
  với thép trên.

### Chưa kiểm được

- **Trên AutoCAD thật:** co / tê, móng băng, cọc cuối, dòng "đã nạp" bằng English.
- **Bộ cài Inno thật.**
- **File `.xlsx` thật** do Excel tiếng Việt lưu.

Lượt soát mất khoảng 25 phút. Chi phí cả thread khoảng $25,6.

### Lượt sửa 2 (luồng chính ghi, 27/11 00h25)

- **Chia việc:**
  - **A**, nhánh `full/nang-cap-h-sua-ktp-a`: KTP-04, 41, 42, 43, 44, 47a.
  - **B**, nhánh `full/nang-cap-h-sua-ktp-b`: KTP-38, 40, 45, 46, hai điểm nhỏ ở KTP-17 / 29, chuẩn bị KTP-20.
  - **C**, ngay trên `full/nang-cap-h-tong-the-2`: KTP-37, 39, 48, 24, cọc cuối, 47 b / c / d, rồi gộp hai nhánh kia và chạy CI.
- **Quyết định luồng chính tự chọn:**
  - **KTP-40:** khi nhịp ngắn thì đặt một đai ở đầu; giữ nhịp tối thiểu 100 mm, không đổi số khách gặp.
  - **KTP-44:** thêm "xoá / xoa", "ghi đè", "thay thế", "overwrite", "replace", "ơi", "yeah", "alright", "fine", "👍" vào danh sách đồng ý.
    "được thôi" và "ok thôi" là đồng ý; "thôi" đứng riêng là từ chối.
  - **KTP-47a:** gõ riêng "thôi được" là đồng ý; "không sao" là không đồng ý, vì có chữ "không".
- **Hỏi chủ dự án:** KTP-20, khe hở giữa hai thanh thép dầm.
