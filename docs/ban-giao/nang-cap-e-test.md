# Bàn giao: Test & sửa Nâng cấp đợt E (cầu thang, mái)

*13/11/2026 · thread Test & sửa đợt E (con của Nâng cấp đợt E) · nhánh `full/nang-cap-e-test` (xuất phát `full/nang-cap-e`
@ `3e8a67c`). **Chưa thử trên AutoCAD thật.** Thử tay: mục **R** của [`checklist-thu-that.md`](../checklist-thu-that.md)
(R14–R16 là các lỗi dưới đây).*

## Lỗi tìm thấy → sửa

| # | Lỗi (người dùng sẽ thấy gì) | Sửa | Ở đâu |
| --- | --- | --- | --- |
| NE-1 | **Đặt cầu thang vào ô mà nói hướng đi lên thì hướng bị bỏ qua**: "dựng cầu thang trong ô này, đi lên theo trục Y" → Claude gửi `well_handle` + `direction`, plugin tự chọn góc / hướng theo cạnh dài, có thể đi ngược ý khách | Không có `start` mà có `direction`: chỉ thử các cách đặt có vế 1 đi đúng hướng đó (lệch ≤ 1°). Vừa ô nhưng không theo hướng đó → báo lỗi rõ "vừa ô nhưng không đi lên theo hướng …", không dựng. Mô tả lệnh thêm "(keeping direction when given)" | `Core/StairRoof.cs` (`FitInWell`), `Drawing/StairRoofTools.cs`, `agent/src/tools/solids3.ts` |
| NE-2 | **Đường bao vẽ tay lệch 1 mm thành "nhà chữ L"**: một đỉnh thừa giữa cạnh lệch dưới 1 mm (hoặc bậc 1 mm ở góc) làm mái 4 dốc / 2 dốc báo "đường bao có góc lõm, nhà chữ L / T / U", ô cầu thang mất góc vuông nên cầu thang báo không vừa | `Clean` bỏ đỉnh cách đường nối hai đỉnh kề ≤ **1 mm** (`JogTol`, chỉ khi đỉnh nằm giữa, không phải mũi nhọn); góc coi là vuông khi lệch ≤ ~0,1° (`SquareTol`, dùng cho chữ nhật của mái 2 dốc và góc ô cầu thang; trước 0,006°). Bậc lệch thật 300 mm vẫn là nhà chữ L | `Core/StairRoof.cs` (`RoofLogic.Clean`, `IsRectangle`, `StairLogic.FitInWell`) |
| NE-3 | **Ô cầu thang đúng bằng chiều dài các bậc bị báo không vừa** (thiếu 100 mm): phần bậc trên cùng gác 100 mm lên sàn tầng trên bị tính là phải nằm trong ô. Vd ô 1000 × 5040 cho thang 1 vế cao 3000 (18 bậc × 280) → báo cần 5140 | Mép gác lên sàn trên là phần riêng (`top ledge`, `OnUpperFloor`) không tính khi kiểm vừa ô và khi đặt vào góc ô; khối vẫn dựng đủ (gác 100 mm ra ngoài mép ô, đúng chỗ sàn trên). `footprint_mm` giờ là kích thước cần trong ô (thang 1 vế: số bậc × rộng bậc, không cộng 100); ghi chú kết quả nói mép bậc trên cùng vượt `footprint_mm` / mép ô | `Core/StairRoof.cs` (`Build`, `Outside`, `PlacedFootprint`), `StairRoofTools.cs` |

Đã rà, không phải lỗi: biên dạng vế thang (135 tổ hợp cao tầng 2700 / 3000 / 3300 / 3600 / 4500 × cao bậc 100 / 160 / 250 ×
rộng bậc 150 / 280 / 500 × bản thang 50 / 150 / 500, cả 3 kiểu: không tự cắt, mặt bậc đặc, trên bậc trống; cao bậc thực ra
ngoài 100–250 thì từ chối); `first_flight_risers` biên (2 và n − 2; chữ U vế 2 dài hơn vế 1, khe 0); lan can khi rẽ phải
(chữ L / U, trái / phải đúng phía trong bản vẽ); ô cầu thang xoay chéo, vẽ theo chiều kim đồng hồ, có điểm trùng / điểm giữa
cạnh, đúng bằng kích thước; mái trên hình chữ nhật cùng chiều kim đồng hồ có điểm thừa (2 dốc), 4 dốc trên tam giác ở dốc
5° / 60° so với công thức mặt phẳng; nhãn ThreeD, không qua thẻ (chỉ dựng khối mới, chỉ đọc ô / đường bao); hoàn tác (lệnh
chạy trong transaction của lượt, lỗi giữa chừng thì hủy cả lệnh); đơn vị mét (`start`, ô, đường bao theo đơn vị bản vẽ ×
INSUNITS; `base_z`, `wall_top_z` và mọi số đo là mm như các lệnh 3D khác).

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **856** test (702 trước; 135 là các trường hợp của theory biên dạng
  thang). `NangCapETestSuaTests.cs`: biên dạng mọi tổ hợp; lan can rẽ phải; chữ U biên; ô xoay / ngược chiều / điểm thừa /
  đúng kích thước; NE-1 (4 hướng trong ô vuông, ô hẹp không đi ngang được, plugin truyền `direction`); NE-3 (3 kiểu thang
  vừa ô đúng bằng các bậc, mép gác vẫn dựng, ô ngắn 100 vẫn từ chối); NE-2 (đỉnh lệch 0,8 mm / 0,4 mm / bậc 1 mm → chữ nhật,
  bậc 300 mm vẫn từ chối, mũi nhọn bị bỏ, góc vát 50 giữ); mái 2 dốc / 4 dốc tam giác. Sửa 3 dòng test cũ theo thay đổi có
  chủ đích NE-3 (`Length` thang 1 vế, `Breadth` chữ L, mép cuối vế 2 chữ U).
- Trợ lý: `cd agent && npm ci && npm test` — **287** test (286 trước). `test/nangcapetest.test.ts` (1): mô tả nói giữ hướng
  khi đặt vào ô, schema nhận `well_handle` + `direction`.
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`): xanh, 0 cảnh báo.
- CI: run **43465289107 xanh** ở `e7e8544` (`release: "false"`, Windows: trợ lý, test lõi, UI test WPF, build 3 bản, gói + Setup.exe; bước phát hành bỏ qua đúng).

Token: thêm ~35 ký tự vào mô tả `build_stair` (gói 3D / Full, ~10 token); system prompt không đổi.

## Còn phải thử tay (chỉ thấy trên AutoCAD thật)

R1–R13 của đợt E và R14–R16. Đặc biệt: hướng đùn region và `AlignCoordinateSystem` (vế thang nghiêng đúng chiều, không bị
lật), hợp khối các vế + chiếu nghỉ (một khối), giao / trừ các nêm lớn của mái, bản vẽ đơn vị mét.

## Giới hạn còn lại (không sửa ở bước này)

- Như bàn giao đợt E: vế thẳng, bậc đều; không thang xoắn / rẻ quạt; lan can không có ở chiếu nghỉ; không tự khoét sàn trên;
  mái 4 dốc chỉ đường bao lồi, 2 dốc chỉ chữ nhật; `from_walls` lấy hình chữ nhật bao tường.
- Phần cuối bản thang (dưới chiếu nghỉ / sàn trên) cắt đứng ở mép vế + 100 mm nên thò dưới đáy chiếu nghỉ / sàn một đoạn
  ngắn (≈ 100–200 mm), như cách vẽ cũ; không ảnh hưởng mặt bậc.
- Đặt vào ô không có `direction`: plugin tự chọn (cạnh dài trước); khách muốn hướng cụ thể thì nói hướng hoặc điểm bắt đầu.
- Chữ U có số bậc lẻ: mép gác 100 mm của vế 2 nằm trong hình chữ nhật ô (cạnh vế 1), vẫn ở cao độ sàn trên.

## Commit

`e7e8544` (sửa NE-1…NE-3 + test + tài liệu, CI 43465289107 xanh), sau đó một commit ghi kết quả CI vào file này (chỉ tài liệu).
