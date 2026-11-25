# Thử tay đợt F3: mái nhà chữ L / T, vật liệu, ảnh, góc nhìn, số mặc định mới

*Nhánh `full/nang-cap-f3`. Chi tiết: `docs/ban-giao/nang-cap-f3.md`. Cần máy **gói 3D hoặc Full** (bộ cài thử nội bộ là
Full), một bản vẽ trống đơn vị mm. Khoảng 30 phút.*

Gõ trong khung chat như khách. "Đạt" là thấy đúng như cột phải; khác thì chụp màn hình và ghi số thứ tự.

## A. Tự kiểm (~3 phút, làm trước)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| A1 | Gõ lệnh `CLAUDECHATSELFTEST` trong AutoCAD | Dòng "Nâng cấp đợt F3: mái 4 dốc nhà chữ L một khối, vật liệu, góc nhìn đã lưu + ảnh PNG" **đạt** (chữ xanh) |

## B. Mái nhà chữ L / T (~12 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| B1 | "Vẽ đường bao nhà chữ L: cánh dài 12 m × 6 m, cánh ngắn 6 m × 6 m nhô ra ở một đầu; dựng tường cao 3,3 m rồi dựng mái 4 dốc" | Một khối mái (bấm vào là chọn cả mái). Chỗ hai cánh gặp nhau có **khe mái** (đường chéo lõm vào), không có hai mái chồng lên nhau |
| B2 | Xoay góc nhìn 3D, nhìn từ dưới lên (hoặc cắt ngang) chỗ góc trong của chữ L | Mặt dưới mái nằm **sát đỉnh tường** quanh nhà, không hở, không có mảnh mái thò xuống trong nhà |
| B3 | "Lập bảng khối lượng mái" | Thể tích mái khoảng **diện tích mặt bằng mái × 0,139 m** (bản 120 mm, dốc 30°), không bị gấp đôi chỗ nối |
| B4 | Hoàn tác, rồi "dựng mái 2 dốc" trên cùng nhà | Một khối mái: **một nóc chính** chạy dọc cánh dài, cánh ngắn có nóc riêng chạy vào mái chính; đầu hồi để hở |
| B5 | Vẽ nhà chữ T (thanh ngang 14 × 5 m, chân 5 m rộng × 6 m dài), dựng mái 4 dốc | Một khối, hai khe mái ở hai góc trong |
| B6 | Vẽ nhà có một bức tường xiên lõm vào (góc không vuông), nhờ dựng mái 4 dốc | Claude báo chưa dựng được loại này, gợi ý mái bằng hoặc vẽ lại góc vuông. Bản vẽ không có gì mới |
| B7 | Nhà hình chữ nhật 10 × 6 m, mái 4 dốc | Như trước đợt F3 (chỉ bản mái mỏng hơn: 120 mm) |

## C. Số mặc định mới (~5 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| C1 | "Dựng cầu thang chữ U cao 3,3 m" (không nói độ dày) | Bản thang và chiếu nghỉ dày **120 mm** (đo bằng lệnh DIST hoặc hỏi Claude); khe giữa 2 vế **100 mm** |
| C2 | "Dựng mái 2 dốc" (không nói độ dày) | Bản mái dày **120 mm** (đo vuông góc mặt mái) |
| C3 | "Dựng mái bằng" (không nói độ dày) | Sàn mái dày **150 mm** |
| C4 | Chạy một macro cũ có dựng cầu thang / mái (ghi trước đợt F3, có số độ dày) | Ra **đúng như lần ghi** |

## D. Vật liệu và ảnh (~7 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| D1 | Trên nhà ở mục B: "gán vật liệu: tường bê tông, mái ngói, cửa sổ kính" | Claude gán xong, báo số khối đã gán. Gõ `VISUALSTYLES` → chọn **Realistic** (hoặc nhờ Claude "đổi kiểu hiển thị realistic"): tường xám, mái đỏ gạch, kính xanh nhạt nhìn xuyên được |
| D2 | "Gán vật liệu gỗ màu nâu đậm tên Gỗ lim cho cửa đi" | Có vật liệu "Gỗ lim" mới (gõ `MATBROWSEROPEN` → Document Materials thấy tên này), cửa đổi màu |
| D3 | "Xuất ảnh phối cảnh có vật liệu" | Claude báo đường dẫn file **.png** cạnh bản vẽ. Mở file: thấy nhà có màu vật liệu. **Màn hình AutoCAD không đổi** góc nhìn |
| D4 | Khóa layer tường (`LAYER` → khóa), nhờ "gán vật liệu gạch cho tường" | Claude báo bỏ qua các khối trên layer khóa, không tự mở khóa |
| D5 | Máy gói **2D** (hoặc máy Full chọn chế độ **2D**): "gán vật liệu bê tông" | Claude nói không làm được ở gói / chế độ này (chế độ: bảo đổi sang 3D hoặc Cả hai, không mời mua gói) |

## E. Góc nhìn đã lưu (~3 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| E1 | Xoay tới một góc nhìn đẹp, "lưu góc nhìn này tên Phối cảnh 1" | Gõ `VIEW`: thấy "Phối cảnh 1" trong danh sách |
| E2 | Xoay đi chỗ khác, "về góc nhìn Phối cảnh 1" | Màn hình trở lại đúng góc đã lưu |
| E3 | "Lưu góc nhìn tên Phối cảnh 1" lần nữa | Claude hỏi có ghi đè không (không tự ghi đè) |
| E4 | "Xuất ảnh từ góc nhìn Phối cảnh 1", rồi "xóa góc nhìn Phối cảnh 1" | Có file PNG theo đúng hướng nhìn đó; sau khi xóa, `VIEW` không còn tên này |
