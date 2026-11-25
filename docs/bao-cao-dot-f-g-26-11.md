# Báo cáo: đợt F và đợt G (25–26/11/2026)

*Luồng chính. Đợt F chạy 14h27 → 21h15 ngày 25/11; đợt G chạy 21h25 ngày 25/11 → 05h24 ngày 26/11 (giờ Việt Nam), theo yêu
cầu "chạy liền, không hỏi lại, dừng trước phát hành" và "test lại một lượt bằng agent khác trước khi gửi bộ cài".*

## 1. Tóm tắt

- **Xong 10 phần việc:** đợt F (F1 → F5) và đợt G (G1 → G5). Mỗi phần có một thread viết và một thread khác rà + sửa, CI
  xanh ở từng bước.
- **Kiểm tra cuối:** test tổng thể hai lần (cuối đợt F, cuối đợt G); kiểm tra độc lập bằng agent mới hai lần (đợt F; rồi
  cả F + G theo yêu cầu của cậu, soát lại ba lượt).
- **Nhánh chốt:** `full/nang-cap-g-tong-the` @ `547d6af`. Test lõi 1445, trợ lý 407 (trước đợt F: 910 và 298), build đủ
  3 bản cho AutoCAD 2022–2027, CI Windows xanh (run 45710924398).
- **Lỗi:** 97 lỗi được ghi qua các bước kiểm, 96 đã sửa và soát lại, 1 lỗi nhẹ chấp nhận (mục 7).
- **Chưa phần nào chạy trên AutoCAD thật, Claude thật hay Excel thật.** Chưa phát hành: `main` vẫn là v0.9.3 của bản gốc.
- **Bộ cài thử bản 5** (có đủ đợt F + G): build xong 05h36 ngày 26/11, CI xanh (run 45713707097). Link tải và cách cài nằm ở thread
  ["Lưu trữ bộ cài"](https://claude.ai/code/session_01D56HmJdn7RASaJtfgMQp5x) và mục 3.

## 2. Plugin có thêm gì

| Đợt | Khách được gì | Gói |
| --- | --- | --- |
| F1 | Vẽ đúng chỗ khi hệ tọa độ (UCS) đang xoay hoặc dời; nút chọn chế độ "Cả hai / 2D / 3D" | UCS: mọi gói. Nút chế độ: máy gói Full |
| F2 | Bộ style chuẩn Việt Nam (chữ, kích thước, nét); trang in có khung tên; chèn block từ thư viện có sẵn (cửa, thiết bị vệ sinh, đồ nội thất, cây); lưu / gọi lại trạng thái layer | 2D / Full |
| F3 | Mái dốc liền khối cho nhà chữ L / T / U; gán vật liệu và xuất ảnh PNG; lưu góc nhìn; bản thang và bản mái dốc dày 120 mm | 3D / Full |
| F4 | Mở chat nhanh và nhẹ hơn (trợ lý khởi động sẵn, chỉ nạp mô tả của lệnh hay dùng); xuất 4 bảng ra Excel | Mọi gói (bảng nào theo gói đó) |
| F5 | Ô bật / tắt "Nạp gọn lệnh" trong ⚙; thẻ Đồng ý / Hủy trước khi ghi đè file, góc nhìn, vật liệu cả layer; ghi chú có mũi tên theo bộ chuẩn | Mọi gói. Ghi chú mũi tên: 2D / Full |
| G1 | "Ra bộ hồ sơ": mặt bằng từng tầng, 4 mặt đứng, 2 mặt cắt, phối cảnh lên các tờ có khung tên, in một file PDF | Full: đủ. 2D: tờ mặt bằng 2D + PDF. 3D: các hình trong Model |
| G2 | "Soát bản vẽ": 22 nhóm lỗi theo ba mức; 9 nhóm sửa tự động được sau khi bấm Đồng ý; xuất danh sách ra Excel | 2D / Full |
| G3 | "Lập dự toán": khối lượng × đơn giá cậu / khách đưa, file Excel có công thức. Claude không tự đặt giá | Full: đủ. 3D: từ khối 3D. 2D: từ block và diện tích |
| G4 | "Render": ảnh render thật của AutoCAD. Render lỗi thì tự lưu ảnh chụp thay và nói rõ | 3D / Full |
| G5 | "Vẽ lại bản này" từ ảnh chụp, bản scan, PDF scan: gắn ảnh làm nền, đặt tỉ lệ theo một kích thước thật, vẽ đè lên. Kết quả là bản nháp cần kiểm | 2D / Full |

**Token mỗi lần mở chat** (ước tính, số thật phải đo trên máy thật):

| Gói | Trước đợt F | Cuối đợt G (nạp gọn, mặc định) | Cuối đợt G (tắt nạp gọn) |
| --- | --- | --- | --- |
| 2D | 38,8k | 13,7k | 45,5k |
| 3D | 39,8k | 19,1k | 45,2k |
| Full | 57,7k | 21,0k | 66,8k |

## 3. Việc cần cậu làm

1. **Cài bộ cài thử bản 5.** [Tải `AgentToAutoCADFull-ThuNoiBo-nang-cap-5-0.9.4`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45713707097/artifacts/15069481207)
   (khoảng 135 MB, tải được tới 23/02/2027; mở link bằng trình duyệt đã đăng nhập GitHub).
   - Cài đè lên bản 3 được, **nhưng phải xóa 4 thư mục skill cũ trước khi chạy bộ cài**: bộ cài không ghi đè skill đã
     có trên máy, quên bước này thì Claude vẫn dùng hướng dẫn cũ, thiếu phần đợt F / G.
   - Hướng dẫn từng bước: thread ["Lưu trữ bộ cài"](https://claude.ai/code/session_01D56HmJdn7RASaJtfgMQp5x), hoặc mục "Bản 5" của
     [`ban-thu-noi-bo.md`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/blob/full/ban-thu-noi-bo/docs/ban-giao/ban-thu-noi-bo.md).
   - Cài xong: thanh trên cùng khung chat ghi "nâng cấp 5"; bấm **Chat mới**; gõ `CLAUDECHATSELFTEST` để plugin tự kiểm.
2. **Thử tay** theo
   [`checklist-tong-the.md`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/blob/full/nang-cap-g-tong-the/docs/checklist-tong-the.md)
   (trên nhánh chốt), theo thứ tự:
   - mục **G0** trước (~35 phút): 6 việc rủi ro nhất, xem mục 4;
   - phần còn lại của khối **"Đợt G"** (cả G0 khoảng 3 giờ);
   - khối **"Đợt F"** (~2 giờ). Các mục ghi "Máy khác" bỏ qua được (cần máy gói 2D / 3D).

   Mục nào hỏng: gửi tớ mã mục (vd G0-3) kèm ảnh hoặc chữ ở dòng lệnh.
3. **Duyệt con số** (mục 5).
4. **Xem các quyết định tự chọn** (mục 6) và các điểm đã biết (mục 7).
5. Việc cũ vẫn chờ cậu: dựng máy chủ kích hoạt riêng; quyết định phát hành. Bộ cài thử và bản dùng thử AutoCAD hết hạn
   khoảng 10/12/2026.

## 4. Thử trước: các điểm rủi ro nhất

| Mục | Vì sao rủi ro | Nếu hỏng thì |
| --- | --- | --- |
| **G4 render** (G0-2, G0-3) | Lệnh render của AutoCAD hỏi vài câu ở dòng lệnh. Thứ tự câu hỏi được viết theo trí nhớ, chưa đối chiếu với AutoCAD thật | Plugin tự lưu ảnh chụp thay và nói rõ. Cậu chép các câu AutoCAD hỏi gửi tớ, sửa nhanh |
| **G5 vẽ lại từ ảnh** (G0-4) | Chưa biết Claude đọc tường, chữ, kích thước trên ảnh chính xác tới đâu. PDF scan có thể hiện trắng | Claude nói "không thấy" và nhờ xuất PNG / JPG, không vẽ mò |
| **G1 chạy lại bộ hồ sơ** (G0-5) | Thẻ "thay bộ cũ" và việc xóa đúng tờ cũ chỉ mới qua test tự động | Bấm U một lần là hoàn tác cả lượt |
| **G3 mở file dự toán bằng Excel thật** (G0-6) | Test mới đọc lại cấu trúc file, chưa mở bằng Excel | — |
| **F4 nạp gọn lệnh** (F1, F9, F11) | Chưa biết Claude thật có luôn tìm đúng lệnh ít dùng không | Tắt ô "Nạp gọn lệnh" trong ⚙ rồi bấm Chat mới |

## 5. Con số chờ cậu duyệt

Cậu dặn chạy liền, nên các thread dùng số đề xuất và để mỗi nhóm số ở một chỗ dễ đổi. Bảng đầy đủ (số nào, khách thấy
gì, vì sao):

- [`dot-f-cho-duyet.md`](dot-f-cho-duyet.md): đợt F;
- [`dot-g-cho-duyet.md`](dot-g-cho-duyet.md): đợt G.

Các số cậu dễ gặp nhất:

| Phần | Số đề xuất |
| --- | --- |
| F2 style chuẩn | Chữ kích thước và ghi chú cao 2,5 mm khi in; tiêu đề 5 mm; tên phòng 3,5 mm; font Arial; tỉ lệ mặc định 1:100; khổ mặc định A3 ngang |
| F2 khung bản vẽ | Lề trái 25 mm, ba cạnh còn lại 5 mm; khung tên rộng 180 × cao 52 mm, 10 ô. *Người rà lưu ý: tiêu chuẩn ISO 5457 / TCVN 7285 có thể là 20 / 10 mm, chưa tra lại văn bản* |
| F2 block thư viện (mm) | Cửa đi 900; cửa 2 cánh 1400; cửa sổ 1200; giường 1600 × 2000; bàn 1200 × 800… (15 loại) |
| F3 | 8 vật liệu mẫu (bê tông, gạch, vữa trắng, kính, gỗ, ngói, thép, đá); ảnh PNG cạnh dài 1568 điểm; tay vịn cầu thang cao 900 mm (lan can cậu đã duyệt là 1100) |
| F4 | 25 lệnh hay dùng nạp sẵn; tên file Excel `<bản vẽ>-thong-ke-block.xlsx`, `-dien-tich.xlsx`, `-khoi-luong.xlsx`, `-va-cham.xlsx` |
| F5 ghi chú mũi tên | Chữ 2,5 mm; đầu mũi tên đặc 2,5 mm |
| G1 bộ hồ sơ | Số bản vẽ `KT-01`, `KT-02`…; tỉ lệ 1:100; mặt bằng cắt ở cao 1200 mm; 4 mặt đứng, 2 mặt cắt; tối đa 30 tờ, 10 tầng; file `<bản vẽ>-bo-ho-so.pdf` |
| G2 soát bản vẽ | Hai hình lệch ≤ 0,1 mm là trùng; mỗi nhóm đưa 5 ví dụ; quét tối đa 20 000 hình, sửa tối đa 2 000 hình mỗi lần |
| G3 dự toán | Đơn vị "đồng", làm tròn đến đồng; khối lượng 3 số lẻ; đơn giá lẻ hiện 2 số lẻ trong Excel; file `<bản vẽ>-du-toan.xlsx` |
| G4 render | Ảnh 1920 × 1080; ba mức nhanh / vừa / đẹp, chờ tối đa 120 / 300 / 900 giây rồi lưu ảnh chụp thay; nền trắng |
| G5 vẽ lại từ ảnh | Tối đa 8 lần xem ảnh mỗi yêu cầu; file tối đa 50 MB; tường dày 200 mm khi không đọc được; ảnh dán giữ 7 ngày; layer ảnh nền `X-ANH-GOC` |

Nhiều số (thời gian chờ render, số lần xem ảnh, cỡ chữ khi in) chỉ đánh giá được khi thử thật. Muốn đổi số nào cậu nói
tên số và giá trị mới.

## 6. Quyết định tớ tự chọn lúc sửa lỗi kiểm tra độc lập

Các thread tự chọn nhiều điểm nhỏ, đã ghi ở các bảng "Quyết định tự chọn" trong hai file trên. Riêng sáu điểm dưới đây
do luồng chính quyết để chuỗi không dừng:

| # | Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- | --- |
| 1 | Dự toán: ghép tên hạng mục với tên trong bảng giá | Giữ dấu tiếng Việt ("Sân" không ăn giá của "Sàn"). Gõ không dấu hoặc dùng mẫu `*` mà khớp nhiều dòng khác nhau thì để trống và hỏi lại | Cho ghép lỏng hơn (dễ tính nhầm tiền) |
| 2 | Dự toán theo diện tích, đường bao nhà chứa các phòng bên trong | Không tính, Claude hỏi bỏ đường bao hay cứ tính (tránh tính tiền hai lần) | Cứ tính và chỉ cảnh báo |
| 3 | Vẽ lại từ ảnh: ảnh nằm ở ổ mạng | Chỉ mở file trên máy, file cậu kéo vào khung chat, hoặc file cùng thư mục chia sẻ với bản vẽ. Chỗ khác thì Claude nhờ kéo file vào chat | Cho mở mọi đường dẫn mạng (kém an toàn) |
| 4 | Render: lệnh render báo xong mà chưa thấy file ảnh | Chờ thêm (20 giây, hoặc bằng thời gian ước lượng của mức nếu lâu hơn) rồi mới lưu ảnh chụp thay | Đổi một số ở một chỗ, sau khi cậu thử mức "đẹp" trên máy thật |
| 5 | Soát bản vẽ: tab `Layout1`… chỉ có khung nhìn AutoCAD tự tạo | Không báo lỗi, chỉ nêu tên tab. Nói "soát cả Layout1" thì soát | Coi luôn là tờ in (báo nhiễu ở hầu hết bản vẽ) |
| 6 | Còn một lỗi nhẹ sau lượt soát cuối (mục 7, dòng đầu) | Không sửa thêm, vì phần sửa mới sẽ không còn được agent độc lập kiểm | Sửa ở đợt sau |

## 7. Điểm đã biết, giữ nguyên

- **Lỗi nhẹ chấp nhận (KFG-43):** "soát bản vẽ" chậm khoảng 6 giây khi bản vẽ có khoảng 20 000 điểm hoặc block chồng
  thẳng đứng (cùng chỗ trên mặt bằng, khác cao độ). Rất hiếm, không treo máy.
- Nét của hình mặt cắt / hình chiếu bị COPY đè lên chính nó thì "soát bản vẽ" không báo trùng.
- File ảnh lớn kéo vào chat từ ổ mạng: AutoCAD đứng chờ lúc Claude gắn ảnh (tối đa 50 MB).
- Danh sách file cậu kéo vào chat chỉ nhớ trong một phiên AutoCAD; mở lại AutoCAD thì kéo lại file ở ổ mạng.
- Xuất Excel đổi chữ trông như số (vd "1.10") thành số. Muốn giữ là chữ cho các ô kiểu mã thì nói.
- Bảng thống kê / diện tích luôn nằm ngang theo màn hình, không xoay theo UCS.
- Kích thước bản vẽ mét hiện "4", "4.5", không phải "4.00".
- Ảnh PNG xuất từ góc nhìn đã lưu giữ hướng nhìn, không giữ độ phóng to.
- **Cần quyết trước khi phát hành:** bộ cài chỉ chép skill khi máy chưa có (để không xóa phần khách tự sửa). Khách nâng
  cấp từ bản cũ sẽ không tự nhận skill mới. Bản thử thì cậu xóa tay 4 thư mục; bản phát hành cần một cách tự động.
- Tớ không đưa được file bộ cài vào thẳng thread: mạng của môi trường cloud chặn máy chủ chứa file của GitHub
  (`productionresultssa11.blob.core.windows.net`). Vì vậy gửi bằng link như các bản trước.

## 8. Kiểm tra đã làm

| Bước | Kết quả |
| --- | --- |
| Rà + sửa từng phần (10 phần, người rà khác người viết) | 40 lỗi sửa: F1 4, F2 7, F3 2, F4 3, F5 2, G1 4, G2 4, G3 3, G4 6, G5 5. Mỗi phần CI xanh |
| Test tổng thể đợt F và đợt G | 3 + 2 lỗi chéo giữa các phần, đã sửa |
| Kiểm tra độc lập đợt F (agent mới) | 9 lỗi, 9 sửa đúng sau 3 lượt soát lại. Lỗi nặng nhất: mái nhà chữ L / T / U dựng sai khi đường bao vẽ lệch khoảng 1 mm |
| Kiểm tra độc lập F + G (agent khác, cậu yêu cầu) | Lượt đầu 27 lỗi; ba lượt soát lại thấy thêm 16 lỗi nhỏ hơn. Tổng 43 (1 nhiều khả năng nặng, 11 vừa, 31 nhẹ): 42 sửa đúng, 1 nhẹ chấp nhận. 9 lỗi của đợt F vẫn đúng |
| Luồng chính tự kiểm | CI xanh đúng commit chốt `547d6af`; sau commit code cuối `e1455fb` chỉ có tài liệu thay đổi. Bộ cài bản 5: CI xanh đúng commit `c901952`, nhánh đã chứa `547d6af`, phần khác nhánh chốt chỉ là 13 file của bản thử (nhãn, bỏ kích hoạt, hạn dùng); file bộ cài có trên GitHub, 135 MB |

Lỗi nhiều khả năng nặng của lượt cuối: "render góc nhìn đã lưu" có thể báo lỗi không render được. Đã sửa, cần cậu thử
thật (G0-3).

**Chưa kiểm được** (cần máy thật): mọi thứ chạy trong AutoCAD (vẽ, trang in, in PDF, render, gắn ảnh), Claude thật (tìm
lệnh khi nạp gọn, đọc ảnh, giữ luật "không tự đặt giá"), Excel thật, ổ mạng thật, tốc độ với bản vẽ lớn. Bước tự kiểm
`CLAUDECHATSELFTEST` trong AutoCAD kiểm được một phần lớn, là mục đầu của mỗi khối trong checklist.

Chi tiết: [`ban-giao/kiem-tra-doc-lap-dot-f.md`](ban-giao/kiem-tra-doc-lap-dot-f.md),
[`ban-giao/kiem-tra-doc-lap-dot-fg.md`](ban-giao/kiem-tra-doc-lap-dot-fg.md); bàn giao từng phần ở `docs/ban-giao/` trên
nhánh chốt.

## 9. Thời gian và chi phí

- **Đợt F:** khoảng 6 giờ 50 phút (14h27 → 21h15 ngày 25/11).
- **Đợt G:** khoảng 8 giờ (21h25 → 05h24), trong đó gần 3 giờ đứng chờ vì chạm giới hạn sử dụng 5 giờ (23h00 → 23h50 và
  02h37 → 04h50). Bộ cài bản 5: 05h26 → 05h36 (một lượt CI).
- **Chi phí các thread** (số tích lũy mỗi thread tự báo, tính cả đợt F và G):

  | Thread | USD |
  | --- | --- |
  | Thread đợt F1 (viết F1, F4, G2; rà F2, F5, G1, G3, G5; sửa lỗi) | 86,8 |
  | Thread Test & sửa F1 (viết F3, F5, G4, G5; rà F1, F4, G2; sửa lỗi) | 83,6 |
  | Thread đợt F2 (viết F2, G1, G3; rà F3, G4; hai lần test tổng thể; giữ nhánh chốt) | 89,4 |
  | Kiểm tra độc lập đợt F | 16,4 |
  | Kiểm tra độc lập F + G | 35,4 |
  | **Cộng** | **≈ 312** |

  Chưa kể luồng chính và thread bộ cài (thread bộ cài khoảng $0,5 cho bản 5; luồng chính không tách riêng được phần của hai đợt này). Ước lúc lập kế hoạch: đợt F $125–200, F5 + đợt G $150–200.
- **Phần tốn nhất:** ba lượt sửa và soát lại sau kiểm tra độc lập F + G (khoảng $90), và việc ba thread phải đọc lại
  ngữ cảnh rất dài mỗi lần nhận việc mới.
- Tớ không thấy số token chính xác theo từng đợt; các số trên là tiền mỗi thread tự báo.

## 10. Nhánh và tài liệu

| Thứ | Ở đâu |
| --- | --- |
| Mã chốt đợt F + G | `full/nang-cap-g-tong-the` @ `547d6af` (CI 45710924398) |
| Bộ cài thử bản 5 | `full/ban-thu-noi-bo` @ `c901952` (bàn giao `39ca53b`, chỉ thêm tài liệu), CI 45713707097 xanh, test lõi 1455, trợ lý 407. Nhánh này không bao giờ gộp |
| Checklist thử tay | `docs/checklist-tong-the.md` trên nhánh chốt (khối "Đợt F", "Đợt G") |
| Con số chờ duyệt | [`dot-f-cho-duyet.md`](dot-f-cho-duyet.md), [`dot-g-cho-duyet.md`](dot-g-cho-duyet.md) |
| Kế hoạch và tiến độ | [`ke-hoach-nang-cap-lenh.md`](ke-hoach-nang-cap-lenh.md) mục "Đợt F", "Đợt G" |
| Tổng quan dự án | [`tong-quan-du-an.md`](tong-quan-du-an.md) |
