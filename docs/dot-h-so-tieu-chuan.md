# Đợt H phần 2: số theo tiêu chuẩn chờ chủ dự án duyệt

*Lập 26/11/2026 16h13. Thread Test & sửa F1 gom, luồng chính viết lại. Chủ dự án chọn 26/11 "chạy liền, số tiêu chuẩn
hỏi trước": các số dưới đây phải được duyệt **trước khi** viết H6, H7, H8.*

Các số này chỉ là **mặc định khi khách không nói**. Khách luôn nói được số khác trong chat (vd "trắc dọc tỉ lệ đứng
1:200"). Plugin chỉ vẽ và thống kê, **không tính toán** thủy lực hay kết cấu.

**Độ tin cậy.** Thread tra mạng chỉ tìm ra tên tiêu chuẩn, không đọc được bảng số bên trong. Các số "theo tiêu chuẩn"
là thread nhớ theo nội dung tiêu chuẩn. Chỗ ghi **chưa chắc** cần người có bản TCVN chính thức đối chiếu trước khi giao
cho khách.

## H6. Hạ tầng (trắc dọc, trắc ngang, đồng mức, điểm khảo sát)

| Nhóm | Số đề xuất | Khách thấy gì | Căn cứ |
| --- | --- | --- | --- |
| Tỉ lệ trắc dọc | ngang 1:1000, đứng 1:100 (đứng phóng 10 lần cho thấy rõ dốc) | khung trắc dọc | thông lệ hồ sơ đường; tiêu chuẩn thiết kế đường không quy định tỉ lệ vẽ |
| Tỉ lệ trắc ngang | 1:200 cả ngang lẫn đứng | khung trắc ngang | thông lệ (đường đô thị hẹp hay dùng 1:100) |
| Mức so sánh (đường đáy trắc dọc) | số mét chẵn, thấp hơn cao độ nhỏ nhất ít nhất 2 m | chữ "MSS +xx.00" | thông lệ |
| Khoảng cọc | cọc chi tiết 20 m, cọc trăm mét mỗi 100 m, cọc Km mỗi 1 000 m, thêm cọc ở đầu / cuối / đỉnh cong | vị trí trắc ngang, lý trình | thông lệ khảo sát đường; **chưa chắc** điều khoản |
| Vạch lý trình trên mặt bằng tuyến | skill hiện vạch mỗi 50 m; đề xuất giữ 50 m cho mặt bằng, 20 m cho trắc dọc / trắc ngang | vạch trên mặt bằng | cậu chọn: giữ hai số, hay thống nhất 20 m |
| Bề rộng trắc ngang | theo điểm đo; không có thì 15 m mỗi bên tim | độ rộng trắc ngang | thông lệ |
| Khoảng cao đều đồng mức | 1:500 → 0,5 m; 1:1000 → 1 m (vùng bằng 0,5 m); 1:2000 → 1 m | số đường đồng mức | quy phạm đo vẽ bản đồ địa hình; **chưa chắc** bảng |
| Đường cái | cứ 5 đường con thì 1 đường cái (nét đậm, có ghi số) | nét đậm có số | thông lệ bản đồ |
| Số lẻ | cao độ điểm đo 3 số lẻ; cao độ trên trắc 2 số lẻ; khoảng cách cọc 2 số lẻ (m); độ dốc % 2 số lẻ | chữ số trên bản vẽ | thông lệ |
| Cỡ chữ in | chữ thường 2,5 mm, tiêu đề 3,5 / 5 mm trên giấy | cỡ chữ | TCVN 7284, khớp skill hiện có |
| Tọa độ VN-2000 | **chỉ đổi trục** (cột X là hướng Bắc → trục y của AutoCAD; Y là hướng Đông → trục x), **không** đổi múi chiếu / kinh tuyến trục; m → mm (× 1 000) | vị trí điểm | đổi hệ cần thông số tỉnh, dễ sai. Cậu xác nhận cách này |

## H7. Ống MEP 3D

| Nhóm | Số đề xuất | Khách thấy gì | Căn cứ |
| --- | --- | --- | --- |
| Cỡ ống cấp nước | PPR D25 ống nhánh, D32 ống chính tầng | cỡ ống vẽ ra | thông lệ nhà dân dụng; skill có ví dụ PPR D25 |
| Cỡ ống thoát nước | uPVC D60 thoát sàn / lavabo / bồn rửa; D110 xí và ống đứng; D60 thông hơi | cỡ ống vẽ ra | thông lệ; TCVN 4474:1987 có cỡ tối thiểu theo thiết bị (xí ≥ D100), **chưa đối chiếu** bảng |
| Ống PCCC, ống gió | **không mặc định, bắt khách nhập cỡ** (gợi ý nếu khách hỏi: PCCC DN100 / DN50 / DN25; ống gió 300 × 200 mm) | khách phải nói cỡ | cỡ PCCC do thiết kế PCCC quyết; **chưa chắc** |
| Độ dốc ống thoát nước | mặc định 2 %; báo khi khách nhập thấp hơn mức tối thiểu theo cỡ (D50 2,5 %, D100 1,2 %, D125 1 %, D150 0,7 %, D200 0,5 %) | ống dốc, cảnh báo | skill có "i = 2%"; mức tối thiểu theo TCVN 4474:1987 **chưa chắc**. Cấp nước, PCCC, ống gió không dốc |
| Cao độ đi ống | đỉnh ống cách đáy dầm 100 mm; không có dầm thì cách đáy sàn trên 300 mm | vị trí ống | thông lệ |
| Co / tê | ống tròn: co 90° bán kính 1,5 lần đường kính; ống gió: bán kính tâm = 1 lần bề rộng; tê vuông góc | hình co / tê, số co / tê trong thống kê | thông lệ phụ kiện, không có TCVN riêng |
| Khoảng hở khi soát va chạm | 50 mm: ống cách dầm / cột / ống khác dưới 50 mm cũng báo "sát" | danh sách chỗ sát / chạm | thông lệ phối hợp MEP (25–50 mm) |
| Layer / màu | giữ skill: `P-SUPL` 5, `P-HOT` 1, `P-DRAIN` 3, `P-VENT` 8; thêm `M-DUCT` 4, `M-DUCT-RET` 6, `F-SPRK` 1; layer 3D thêm hậu tố `-3D` | tên layer, màu | thông lệ đặt tên layer; PCCC sơn đỏ |
| Thống kê | chiều dài m 2 số lẻ, gộp theo hệ + cỡ ống; co / tê đếm cái; **không cộng hao hụt** | bảng thống kê | cậu chọn có thêm cột hao hụt % không |

## H8. Kết cấu (móng, bản vẽ thép, thống kê thép)

| Nhóm | Số đề xuất | Khách thấy gì | Căn cứ |
| --- | --- | --- | --- |
| Lớp bê tông bảo vệ | sàn 20; dầm 25; cột 25; móng có lót 40; móng không lót 70 (mm); không nhỏ hơn đường kính thanh | mặt cắt, chiều dài thanh | TCVN 5574:2018 (trong nhà 20, ẩm 25, ngoài trời 30, trong đất có lót 40); dầm / cột 25 theo thông lệ; **70 cho móng không lót chưa chắc** |
| Neo / nối chồng | neo thanh chịu kéo 30d; nối chồng 40d (dầm, sàn), 35d (cột); báo khi khách nhập dưới mức tối thiểu (neo ≥ 15d và ≥ 200 mm; nối ≥ 20d và ≥ 250 mm); bản vẽ ghi "kỹ sư kiểm lại chiều dài neo / nối" | bản vẽ thép, bảng thống kê | thông lệ bản vẽ thi công; tiêu chuẩn tính theo công thức; **mức tối thiểu chưa chắc** |
| Móc và uốn | thép trơn: móc 180°, uốn trong 2,5d, đoạn thẳng 3d; thép đai: móc 135°, đoạn thẳng 6d và ≥ 75 mm; thép gân: không móc, bẻ L 10d khi neo | hình móc, chiều dài thanh | thông lệ + TCVN 5574 / 4453; **75 mm chưa chắc** (số của tiêu chuẩn Mỹ) |
| Khối lượng thép | 0,00617 × d² kg/m (Ø6 0,222 … Ø32 6,31); chiều dài thanh làm tròn 10 mm; khối lượng kg 2 số lẻ; tổng theo đường kính và loại thép; **không cộng hao hụt** | bảng thống kê | **chắc chắn** (TCVN 1651:2018). Cậu chọn có cột hao hụt % không |
| Đường kính, loại thép | 6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 28, 32; Ø ≤ 10 thép trơn CB240-T, Ø ≥ 12 thép gân CB400-V | ký hiệu thép | TCVN 1651 |
| Thép mẫu | sàn Ø8 a200; đai dầm Ø6 a150 ở 1/4 nhịp gần gối, a200 đoạn giữa; đai cột Ø6 a200; cây thép dài 11,7 m (đếm mối nối) | khi khách không nói | thông lệ. **Plugin không tự chọn số thanh / đường kính thép chủ** (đó là tính toán): khách phải nhập, vd "dầm 2Ø18 dưới, 2Ø16 trên" |
| Cỡ móng mẫu | móng đơn 1 200 × 1 200, đế cao 350, cổ móng bằng cỡ cột; móng băng rộng 1 000, cao 300, sườn 300 × 600; bê tông lót dày 100, rộng hơn móng 100 mỗi bên; ghi "cỡ mẫu, kỹ sư quyết" | móng vẽ thử | thông lệ nhà 2–4 tầng; đề xuất bắt khách nhập cỡ thật |
| Tỉ lệ, ký hiệu | mặt cắt dầm / cột 1:20 (hoặc 1:25); triển khai dầm, mặt bằng thép sàn 1:50; mặt bằng móng 1:100; số hiệu thanh trong vòng tròn, ghi "2Ø18 L=5200", "Ø8a200" | bản vẽ thép | thông lệ; TCVN 5572:2012 **chưa chắc** số tỉ lệ |

## Các chỗ cần đối chiếu bản TCVN chính thức

1. Lớp bảo vệ móng không lót 70 mm.
2. Mức tối thiểu chiều dài neo / nối chồng.
3. Bảng độ dốc tối thiểu ống thoát nước (TCVN 4474).
4. Khoảng cao đều đồng mức theo tỉ lệ.
5. Khoảng cọc chi tiết 20 m.
6. Móc đai 135° đoạn thẳng ≥ 75 mm.

## Kết quả duyệt

**26/11/2026 ~19h20, chủ dự án chọn "Dùng hết số đề xuất"** (câu hỏi qua AskUserQuestion). Nghĩa là:

- **Mọi số đề xuất trong ba bảng H6, H7, H8 ở trên là số đã duyệt.** Thread dùng đúng các số đó làm mặc định khi khách không
  nói. Khách vẫn nói được số khác trong chat.
- **Hai điểm "cậu chọn" lấy theo đề xuất:**
  - vạch lý trình: giữ **50 m trên mặt bằng tuyến, 20 m trên trắc dọc / trắc ngang**;
  - thống kê ống (H7) và thống kê thép (H8): **không cộng hao hụt**. Cột hao hụt % để tắt sẵn, đổi lại được.
- **Tọa độ VN-2000:** chỉ đổi trục, không đổi múi chiếu / kinh tuyến trục (đề xuất), m → mm.
- **6 chỗ "chưa chắc"** (mục trên) vẫn dùng số đề xuất. Tài liệu cho khách và bàn giao ghi rõ: "số mặc định, kỹ sư đối chiếu
  TCVN hiện hành trước khi dùng". Các chỗ đó là:
  - lớp bảo vệ móng không lót 70 mm;
  - mức tối thiểu neo / nối chồng;
  - bảng độ dốc tối thiểu ống thoát;
  - khoảng cao đều đồng mức;
  - khoảng cọc 20 m;
  - móc đai 135° ≥ 75 mm.

  Có người đối chiếu bản TCVN chính thức thì chỉ sửa một chỗ trong `H6Numbers` / `H7Numbers` / `H8Numbers`.
- **26/11 ~19h50, chủ dự án chốt thêm cho H7 (độ dốc ống thoát): "Ống nhỏ 2,5 %, ống lớn 2 %".**
  - Lý do: thread rà phát hiện D60 dốc mặc định 2 % thấp hơn mức tối thiểu 2,5 % của cỡ này, nên ống nào vẽ ra cũng bị cảnh báo.
  - Từ nay độ dốc mặc định = max(2 %, mức tối thiểu theo cỡ). D60 → 2,5 %; D110 trở lên → 2 %.
  - Mức tối thiểu theo cỡ giữ như bảng.
- **Giới hạn nói với khách giữ nguyên:**
  - bản vẽ hỗ trợ, kỹ sư vẫn kiểm;
  - không tính thủy lực / kết cấu;
  - plugin không tự chọn số thanh / đường kính thép chủ.
