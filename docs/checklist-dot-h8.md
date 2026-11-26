# Checklist thử tay đợt H8: kết cấu (khoảng 25 phút)

*Đã gộp vào khối "Đợt H phần 2" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt H phần 2), thử tay theo file đó.*

AutoCAD 2022–2027. Phần A, B cần máy gói 2D hoặc Full; phần C cần gói 3D hoặc Full.

*Số mặc định (đã duyệt 26/11/2026):*
- *lớp bảo vệ sàn 20 / dầm 25 / cột 25 / móng có lót 40 / không lót 70 mm;*
- *neo 30d; nối 40d (cột 35d);*
- *đai dầm Ø6 a150 / a200, đai cột Ø6 a200, sàn Ø8 a200;*
- *0,00617·d² kg/m; cây thép 11,7 m.*

*Chỗ ghi "kỹ sư đối chiếu TCVN hiện hành": móng không lót 70, mức tối thiểu neo / nối, móc đai ≥ 75 mm, tỉ lệ TCVN 5572.*

## A. Thép dầm, cột, sàn (~12 phút)

1. Gõ `CLAUDECAD_SELFTEST`. Dòng "Nâng cấp đợt H8: kết cấu …" phải **đạt**.
2. "Vẽ thép dầm 220x400 nhịp 5 m", **không nói thép**: Claude **hỏi** thép chủ (không tự chọn).
3. "Vẽ thép dầm 220x400 nhịp 5 m, 2Ø18 dưới, 2Ø16 trên". Cần thấy:
   - triển khai dầm: hai thanh dọc chạy vào gối mỗi bên 30d (Ø18: 540 mm) rồi bẻ L 10d (Ø18: 180 mm; thanh dưới bẻ lên,
     thanh trên bẻ xuống), đai dày ở 1/4 nhịp hai đầu, thưa ở giữa;
   - mặt cắt bên cạnh: 2 chấm dưới, 2 chấm trên, đai trong lớp bảo vệ 25 mm;
   - số hiệu trong vòng tròn, chữ "2Ø18 L=6440" (5000 + 2 × 540 + 2 × 180), "Ø6a150/a200 …"; layer `S-CONC`, `S-REBAR`, `S-REBAR-TEXT`;
   - Claude nói đai là **thép mẫu**, và nhắc kỹ sư kiểm lại neo / nối.
4. "Neo 150 mm": Claude **cảnh báo** neo dưới mức tối thiểu.
5. "Mặt cắt cột 300x300 cao 3,3 m, 8Ø20": 8 chấm (4 góc + 4 giữa cạnh), đai; chiều dài thanh = 3300 + 35d.
6. "Thép sàn ô 4x3 m" (không nói thép): dùng Ø8 a200 hai phương, có móc 180° (thép trơn), Claude nói là thép mẫu.
   *(sửa KTP-21)* Thanh dài 4000 − 2 × 20 + 2 × 6,25 × 8 = **4060**, thanh ngắn **3060** (mỗi móc cộng 6,25d).
7. "6Ø25 trong dầm rộng 200": Claude báo **không đủ chỗ** một hàng, không tự dồn.

## B. Bảng thống kê thép (~6 phút)

1. "Thống kê thép": mỗi số hiệu một dòng (cấu kiện, hình dạng, Ø, CB240-T / CB400-V, dài 1 thanh làm tròn 10 mm, số thanh,
   tổng dài, kg), dòng "Cộng Ø…" cho từng đường kính, dòng "Cộng CB240-T" / "Cộng CB400-V" cho từng loại thép
   *(sửa KTP)*, dòng Tổng. Kiểm tay một dòng: kg = 0,00617 × d² × tổng dài (m).
2. "Chèn bảng thống kê thép vào bản vẽ tại …": bảng trong bản vẽ.
3. "Xuất bảng thép ra Excel": file `.xlsx`; gọi lại thì Claude hỏi thẻ ghi đè.
4. "Có cộng hao hụt 3%": thêm cột "Khối lượng + hao hụt 3%". Không nói thì **không** có cột này.
5. Cột dài 12,5 m (8Ø20): cột "Số mối nối" đếm 1 mối mỗi thanh (cây 11,7 m); dài 1 thanh cộng thêm nối chồng 35d = 700 mm,
   hình dạng ghi "gồm 1 nối chồng 35d = 700"; kg tính theo chiều dài đã cộng.
6. Vẽ hai dầm cùng `start_mark` 901 nhưng thép khác (2Ø18 và 2Ø20): Claude báo số hiệu trùng; bảng có hai dòng 901
   riêng (không gộp), Claude đề nghị đánh số lại.

## Test & sửa H8 (~3 phút)

1. "Vẽ thép dầm 220x400 nhịp 5000, 2Ø18 dưới, 2Ø16 trên": in khung nhìn tỉ lệ 1:50 thì mặt cắt bên cạnh đọc đúng **1:20** (đo
   bằng thước: bề rộng 220 mm ra 11 mm trên giấy); chữ đai ghi "a150/a200"; thanh Ø18 L=6440.
2. "Vẽ mặt cắt cột 300x300 20Ø25": Claude báo không đủ chỗ, nhờ bớt thanh hoặc tăng cỡ cột.

## Sửa KTP (sau kiểm tra độc lập phần 2, ~3 phút)

1. *(KTP-19)* Dầm nhịp 5000, đai Ø6a150/a200: đai đầu tiên và đai cuối cùng đều cách gối **50**; hai đầu dầm có số đai a150
   bằng nhau; đoạn giữa chia đều (khoảng cách không quá 200).
2. *(KTP-20)* "5Ø20 trong dầm rộng 200": Claude báo **không đủ chỗ** (khe hở giữa hai thanh phải ít nhất bằng một thanh).
3. *(KTP-31)* "Vẽ thép dầm nhịp 50": Claude báo nhịp quá ngắn (ít nhất 100 mm), không ra lỗi lạ của AutoCAD.
   *(lượt 2: KTP-40)* "Vẽ thép dầm nhịp 150": vẽ được, 2 đai cách mỗi gối 50, không lỗi.
4. *(KTP-18)* "Dựng móng băng theo trục (0,0) → (6000,0) **không sườn**": móng băng phẳng, không lỗi.

## C. Móng 3D (~5 phút, gói 3D / Full)

1. "Dựng móng đơn tại (0,0) và (4000,0)" (không nói cỡ): móng 1200 × 1200 đế 350 trên `S-FOOT-3D`, bê tông lót dày 100,
   rộng hơn 100 mỗi bên trên `S-LEAN-3D`; Claude nói "cỡ mẫu, kỹ sư quyết".
2. "Móng đơn 1500x1500 cổ móng 300x300 cao 700": có cổ móng liền đế.
3. "Móng băng theo trục (0,0) → (6000,0) → (6000,4000)": móng băng rộng 1000 cao 300, sườn 300 × 600, góc khép kín.
4. "Bảng khối lượng bê tông theo layer" (`volume_schedule`): có dòng `S-FOOT-3D` và `S-LEAN-3D`.
5. "Soát bản vẽ" (`check_drawing`): các layer `S-…` **không** bị báo "ngoài chuẩn".

## D. Giới hạn nói rõ với khách

- Claude nói rõ: bản vẽ hỗ trợ, không tính kết cấu; không tự chọn số thanh / đường kính thép chủ; kỹ sư vẫn kiểm.
- U một lần xóa hết phần vừa vẽ của lượt.
