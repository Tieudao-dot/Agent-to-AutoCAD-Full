# Checklist thử tay đợt G4: render thật

Máy gói 3D hoặc Full (chế độ "3D" hoặc "Cả hai"), AutoCAD 2022 – 2027. Dùng một bản vẽ có vài khối 3D (vd nhà dựng bằng
`build_3d_from_plan`) đã lưu thành file. Khoảng 20 phút.

## A. Render thường (~6 phút)

1. Gõ: "gán vật liệu gạch cho tường, kính cho cửa sổ, ngói cho mái". Kết quả mỗi lần có câu nói vật liệu lấy từ đâu (màu
   đơn giản hay thư viện Autodesk).
2. Xoay tới góc đẹp, gõ "lưu góc nhìn này tên Phối cảnh 1".
3. Gõ "render góc Phối cảnh 1". Cần thấy:
   - khung chat hiện "đang render … s / khoảng … s" và số giây tăng dần;
   - AutoCAD không treo (vẫn kéo, cuộn được);
   - xong: file `<tên bản vẽ>-Phối cảnh 1-render.png` cạnh bản vẽ, Claude nói đó là **ảnh render**, kèm đường dẫn;
   - nền trắng trong ảnh; sau khi xong, nền khung nhìn và góc nhìn của cậu trở lại như trước.
4. Gõ "render lại góc đó nền trời, chất lượng đẹp". File cũ đã có nên hiện thẻ **Đồng ý / Hủy**. Bấm Hủy thì không có gì
   thay đổi; gõ lại rồi bấm Đồng ý thì render chạy lâu hơn, ảnh có nền xanh nhạt.
5. Bấm U một lần sau khi render: không có thay đổi nào của render cần hoàn tác (nền / ánh sáng đã tự trả lại).

## B. Máy không có thư viện vật liệu (~4 phút)

Trên máy cài AutoCAD **không** kèm thư viện vật liệu Autodesk (hoặc thư mục `Common Files\Autodesk Shared\Materials` trống):

1. "Gán vật liệu bê tông cho sàn": Claude nói dùng vật liệu đơn giản vì máy chưa có thư viện; sàn vẫn có màu bê tông.
2. Trên máy **có** thư viện: mở Materials Browser (`MATBROWSEROPEN`), kéo một vật liệu gạch (vd "Masonry - Brick …") vào bản
   vẽ, rồi gõ "gán vật liệu gạch cho tường": lần này Claude nói dùng vật liệu thư viện, tên vật liệu là tên vừa kéo vào.
3. Vật liệu của cậu đặt tên riêng (vd "Gach-cua-toi") không bị đổi màu hay thay.

## C. Hủy giữa chừng (~4 phút)

1. "Render góc hiện tại chất lượng đẹp cỡ 3840 × 2160" (lâu).
2. Trong lúc khung chat báo "đang render", bấm **Dừng** trong khung chat. Render dừng, không có file mới, nền / góc nhìn trở
   lại như trước.
3. Làm lại, lần này bấm **Esc** trong AutoCAD: cũng dừng, Claude báo cậu đã dừng render.
4. Đang render mà gõ một lệnh khác của AutoCAD: ghi lại AutoCAD làm gì (bước này để biết, không phải lỗi).

## D. Đường lui (~4 phút)

1. Bật "Chỉ xem" (🔒), gõ "render góc hiện tại": vẫn render được, Claude nói dùng nền / ánh sáng của bản vẽ (không đổi gì).
2. Đang chạy một lệnh (vd đang ở giữa lệnh LINE), gõ "render": Claude báo AutoCAD đang chạy lệnh, nhờ bấm Esc rồi thử lại.
3. Đường lui khi render quá lâu: (chỉ thử nếu dễ) render chất lượng "nhanh" cỡ 4096 × 4096 trên máy yếu. Quá thời gian chờ
   tối đa thì render dừng, Claude lưu **ảnh chụp** vào đúng file đó và nói rõ "đây là ảnh chụp, không phải render".
4. Ghi lại nếu AutoCAD hỏi thêm câu nào ở dòng lệnh khi render (lệnh `-RENDER` có thể hỏi theo thứ tự khác ở bản AutoCAD
   này): chụp dòng lệnh gửi tớ.

## F. Thứ tự câu hỏi của `-RENDER` (Test & sửa G4, ~5 phút, quan trọng nhất)

Plugin trả lời `-RENDER` theo thứ tự **viết theo trí nhớ, chưa ai kiểm trên AutoCAD thật**: mức đẹp (`_Low` / `_Medium` /
`_High`) → nơi hiện (`_R` = cửa sổ Render) → rộng → cao → lưu file (`_Y`) → tên file. Cần biết bản AutoCAD của cậu hỏi
đúng thứ tự đó không.

1. Tự gõ `-RENDER` ở dòng lệnh (không qua Claude). Chụp từng câu AutoCAD hỏi và các lựa chọn trong ngoặc vuông, tới khi
   lệnh xong (Esc nếu nó render). Gửi tớ ảnh / chữ của dòng lệnh. **Đây là bước để sửa `RenderLogic.CommandScript` nếu khác.**
2. Gõ "render góc hiện tại chất lượng nhanh". Mở dòng lệnh (F2) xem lại:
   - đúng thứ tự: có ảnh render, Claude nói "ảnh render";
   - sai thứ tự mà một câu trả lời lọt ra dòng lệnh ("Unknown command 1920", "Unknown command Y"…): Claude phải báo **ảnh
     chụp** với lý do "-RENDER asked its questions in another order…", **ngay**, không chờ hết thời gian tối đa;
   - lệnh RENDER xong mà không ra file: sau khoảng 20 giây Claude báo ảnh chụp, lý do "the RENDER command ended without
     writing the picture";
   - lệnh còn đứng chờ nhập (dòng lệnh vẫn đang hỏi): đây là trường hợp duy nhất phải chờ tới thời gian tối đa (nhanh 2 phút,
     vừa 5 phút, đẹp 15 phút); bấm **Dừng** hoặc Esc để thôi sớm. Sau đó dòng lệnh phải trống (không còn lệnh dở).
   Ghi lại trường hợp nào xảy ra.
3. Sau mỗi lần ở bước 2: nền khung nhìn, góc nhìn như trước; không còn file `claudecad-render-*.png` trong thư mục Temp
   (`%TEMP%`) hay `C:\ProgramData\ClaudeCAD\render`.
4. Máy có tên người dùng có dấu cách / dấu tiếng Việt (vd `C:\Users\Nguyen Van A`): render vẫn chạy (file tạm đặt ở
   `C:\ProgramData\ClaudeCAD\render`), không có chữ lạ bị gõ vào dòng lệnh. Không có thư mục nào hợp lệ: Claude lưu ảnh chụp
   ngay và nói rõ.
5. Render góc nhìn có tên chứa dấu tiếng Việt / dấu cách ("Phối cảnh 1"), lưu vào thư mục có dấu cách: file ra đúng tên.

## E. Tự kiểm

Bấm "Tự kiểm" trong khung chat: bước "Nâng cấp đợt G4" phải đạt (nền trắng + đèn đặt tạm rồi trả lại, đường lui ảnh chụp,
vật liệu thư viện có sẵn được dùng).
