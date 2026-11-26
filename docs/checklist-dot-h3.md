# Checklist thử tay đợt H3: so sánh hai bản vẽ

AutoCAD 2022–2027, máy gói 2D hoặc Full, khoảng 30 phút.

**Chuẩn bị:**

- Mở một mặt bằng có sẵn, lưu thành `nha-moi.dwg`. Dùng "Save As" chép ra `nha-cu.dwg` cùng thư mục.
- Trong `nha-moi.dwg`:
  - xóa một bức tường;
  - kéo dài một đường;
  - sửa chữ tên một phòng;
  - đổi layer một cửa;
  - thêm một cửa sổ.

## A. So và báo (~10 phút)

1. Gõ "so bản vẽ này với file nha-cu.dwg" (đường dẫn đầy đủ, hoặc kéo file vào khung chat). Cần thấy:
   - Claude nói **khớp theo handle** (hai file cùng gốc);
   - số **thêm / bớt / đổi** đúng với các thay đổi đã làm (bức tường đã xóa là "bớt", cửa sổ mới là "thêm");
   - mỗi chỗ "đổi" có câu khác ở đâu (đổi hình, đổi layer, chữ cũ → chữ mới);
   - file `nha-cu.dwg` không đổi ngày sửa.
2. Gõ "so lại, file kia là bản mới": thêm và bớt đổi chỗ cho nhau.
3. Mở `nha-cu.dwg` trong AutoCAD thứ hai (hoặc trên máy khác qua thư mục mạng), rồi so lại từ `nha-moi.dwg`. Cần thấy: hoặc so
   được bình thường (kèm ghi chú đọc bản đã lưu), hoặc Claude báo rõ file **đang bị mở / khóa**. Lời báo "có thể hỏng" ở bước
   này là lỗi: ghi lại nguyên câu Claude trả lời.
4. Vẽ lại một mặt bằng gần giống từ đầu (không chép file), lưu `nha-ve-lai.dwg`, rồi so với nó. Claude nói **khớp theo hình
   học**; đường vẽ ngược chiều không bị báo khác.
5. So với chính `nha-moi.dwg`: Claude từ chối, hỏi file khác.
6. So với một file `.pdf` hoặc `.bak`: Claude báo không phải bản vẽ, có gợi ý.

## B. Khoanh chỗ khác (~8 phút)

1. "So với nha-cu.dwg và khoanh chỗ khác". Cần thấy:
   - các đám mây trên layer `X-SO-SANH`: **xanh lá** quanh cửa sổ mới, **đỏ** ở chỗ bức tường đã xóa, **vàng** quanh các chỗ
     đổi;
   - các chỗ khác sát nhau chung một đám mây;
   - mây không đè sát lên đối tượng.
2. Bấm U một lần: mọi đám mây mất.
3. Khoanh lại rồi xóa layer `X-SO-SANH` (lệnh LAYDEL hoặc Layer Properties): hết dấu, bản vẽ còn nguyên.
4. Bản vẽ khác nhiều (vd so với một mặt bằng khác hẳn): Claude hiện **thẻ xác nhận** trước khi vẽ hơn 50 đám mây. Bấm "Không
   đồng ý": không vẽ gì.

## C. Excel và giới hạn (~5 phút)

1. "So với nha-cu.dwg, xuất Excel": có file `nha-moi-so-sanh.xlsx` cạnh bản vẽ, mỗi chỗ khác một dòng (Thêm / Bớt / Đổi, layer,
   khác ở đâu). Gọi lại lần hai thì Claude hỏi thẻ ghi đè file.
2. *(Nếu có bản vẽ rất lớn, trên 20 000 đối tượng)* So: kết quả ghi rõ "chỉ so phần đầu".

## D. Khối 3D (~4 phút, máy gói 3D hoặc Full)

1. Dựng vài khối 3D, lưu bản cũ. Đục một lỗ cửa vào một khối, xóa một khối, rồi so. Cần thấy: khối có lỗ là "đổi" (thể tích cũ
   → mới), khối đã xóa là "bớt".
2. Máy gói 3D: Claude nói chỉ so khối 3D (đường và chữ không so).

## E. SelfTest (~3 phút)

1. Gõ `CLAUDECAD_SELFTEST`. Tìm hai dòng:
   - "Nâng cấp đợt H3: đọc bản vẽ khác chỉ để đọc";
   - "Nâng cấp đợt H3: so sánh hai bản vẽ".

   Cả hai phải đạt. Dòng thứ hai ghi cách khớp (handle / geometry). Ghi lại cách khớp gửi tớ.
