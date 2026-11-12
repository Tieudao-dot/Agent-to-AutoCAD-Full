# Các chỗ còn trỏ về dự án gốc

*Lập ngày 12/11/2026 trên nhánh `claude/inspiring-cray-58xsmt`. Chỉ liệt kê, **chưa sửa chỗ nào**: các chỗ này
quyết định bản Full lấy cập nhật và kích hoạt từ đâu, nên chờ chủ dự án chốt.*

Cách tìm: `git grep` các chuỗi `Agent-to-Autocad`, `Tieudao`, `script.google`, `github.com/`, `CLAUDECHATUPDATE`,
số Zalo liên hệ, cùng tên / mã định danh của bộ cài.

## 1. Máy chủ kích hoạt và khóa (quan trọng nhất)

| Chỗ | Hiện trạng | Hệ quả nếu để nguyên |
| --- | --- | --- |
| `activation/activation.json` → `url` | URL Apps Script của dự án gốc | Bản Full build ra sẽ xin key và kiểm tra trạng thái máy ở máy chủ + Sheet "Thiết bị" của dự án gốc |
| `plugin/ClaudeCAD.Plugin/ClaudeCAD.Plugin.csproj:134` | Nhúng `activation.json` ở trên vào dll | URL gốc nằm cứng trong dll của bản Full |
| `bundle/make-bundle.mjs:95-105` | Chép `activation.json` vào bundle và sinh `activation.iss` cho bộ cài | Bộ cài bản Full cũng gọi máy chủ gốc |
| `plugin/ClaudeCAD.Plugin/Core/License.cs:46-48` | Khóa công khai (`PublicModulus`) của cặp khóa dự án gốc | Key do máy chủ gốc cấp dùng được trên bản Full và ngược lại. Máy chủ riêng cần cặp khóa mới |
| `plugin/ClaudeCAD.Plugin/Core/License.cs:51-52` | Tiền tố dòng ký số `CCL1`, `CCS1` | Dùng chung định dạng với bản gốc. Dòng gói `CCF1` trong kế hoạch là thêm mới |
| `activation/apps-script/Code.gs.template`, `activation/make-apps-script.mjs`, `activation/README.md` | Mẫu và hướng dẫn dựng Apps Script + Sheet | Không trỏ cứng về đâu; dùng lại được để dựng máy chủ riêng |
| `.github/workflows/build.yml:192-194` | CI chặn phát hành nếu `activation.json` không có `url` | Hiện có URL gốc nên bước này **sẽ qua** và phát hành bản gắn máy chủ gốc |

## 2. Nguồn cập nhật (`CLAUDECHATUPDATE`) và Releases

| Chỗ | Hiện trạng | Hệ quả |
| --- | --- | --- |
| `.github/workflows/build.yml:97-98` | Link zip và `latest.json` ghép từ `${{ github.repository }}` | Không trỏ cứng: build ở repo Full thì tự trỏ về Releases của repo Full. Repo Full chưa có bản phát hành nào nên bản build từ đây sẽ không thấy bản cập nhật |
| `.github/workflows/build.yml:180` | Ghi chú bản phát hành trỏ `tree/main/bo-cai` của `${{ github.repository }}` | Tự theo repo Full |
| `.github/workflows/build.yml:6-9, 163-175` | Push `main` có số phiên bản mới là tự phát hành | Merge `main` ở repo Full sẽ phát hành tại repo Full, kèm máy chủ kích hoạt gốc (mục 1) |
| `plugin/ClaudeCAD.Plugin/Sidecar/UpdateService.cs:19, 58` | Đọc nguồn từ `CLAUDECAD_UPDATE_URL` hoặc `Contents\update-config.json` | Không trỏ cứng; phụ thuộc bước build |
| `installer/ClaudeCAD.iss:42` | Chú thích về link `releases/latest/download/ClaudeCAD-Setup.exe` | Chỉ là chú thích |

Người dùng đang chạy bản gốc v0.9.3 vẫn cập nhật từ repo gốc; repo Full không ảnh hưởng tới họ.

## 3. Trùng danh tính với bản gốc trên cùng một máy

**Đã chốt 12/11/2026:** bản Full **thay thế bản gốc như nâng cấp, có cảnh báo**. Giữ danh tính kỹ thuật, chỉ đổi tên hiển
thị thành "Agent to AutoCAD Full". Cách làm: [`ban-giao/test-tong-the.md`](ban-giao/test-tong-the.md); hướng dẫn người
dùng: [`../bo-cai/README.md`](../bo-cai/README.md) mục *Nâng cấp từ ClaudeCAD bản gốc*.

| Chỗ | Hiện trạng | Đã chốt |
| --- | --- | --- |
| `installer/ClaudeCAD.iss` `AppId` | Giống bản gốc | **Giữ**: Windows coi bản Full là nâng cấp bản gốc, một mục trong danh sách ứng dụng |
| `installer/ClaudeCAD.iss` `AppName`, `AppVerName`, `UninstallDisplayName`, các trang | Trước là `ClaudeCAD` | **Đổi** thành "Agent to AutoCAD Full" (hằng `AppDisplayName`) |
| Thư mục `ApplicationPlugins\ClaudeCAD.bundle`, file `ClaudeCAD-Setup.exe` | Giống bản gốc | **Giữ**: cài đè; không cài song song |
| `bundle/PackageContents.xml` `ProductCode` / `UpgradeCode`, `AppName` của thành phần, lệnh `CLAUDECHAT…` | Giống bản gốc | **Giữ**. Chỉ đổi `Name`, `Description`, mô tả thành phần (phần hiển thị) |
| `%LocalAppData%\ClaudeCAD`, `%AppData%\ClaudeCAD` | Chung thư mục trạng thái | **Giữ**: lịch sử chat, cài đặt, skill, macro còn nguyên. Giấy phép cũ không dùng được với khóa của bản Full: bộ cài hiện lại trang kích hoạt. Bản cập nhật bản gốc đã tải chờ cài (`updates\stage`) bị bộ cài bỏ |
| `Contents\edition.json` (mới) | — | Dấu nhận diện bản Full (`bundle/edition.json`): bộ cài và plugin nhận ra bản Full mà không cần link cập nhật |

## 4. Liên hệ hiển thị cho người dùng

Dòng "Liên hệ Zalo owner: …" nằm ở: `activation/activation.json`, `activation/README.md`,
`plugin/ClaudeCAD.Plugin/Core/License.cs:50`, `installer/ClaudeCAD.iss:27`,
`plugin/ClaudeCAD.UI.Tests/PaletteUiTests.cs`, `README.md`, `bo-cai/README.md`. Giữ hay đổi tùy chủ dự án.

## 5. Tài liệu

| File | Chỗ trỏ |
| --- | --- |
| `README.md:20` (dòng gốc, nay lệch vài dòng) | Link tải `ClaudeCAD-Setup.exe` của repo gốc |
| `bo-cai/README.md` | 3 link: tải bộ cài, trang phát hành, trang bản mới nhất của repo gốc |
| `docs/tong-quan-du-an.md` | Link bộ cài thử từ run CI của repo gốc; toàn bộ mục thread và phát hành 0.9.5 |
| `docs/progress.md`, `docs/test-log.md` | Nhật ký lịch sử của dự án gốc, giữ làm lưu trữ |
| `docs/ke-hoach-3d-goi-dich-vu.md` | Bước 0 và điểm chốt 3, 4 nói về phát hành 0.9.5 và thread của dự án gốc |

## 6. Việc cần chủ dự án quyết

1. Máy chủ kích hoạt riêng: Apps Script + Sheet mới, cặp khóa mới. **Đã chốt 12/11/2026:** chủ dự án tự dựng theo
   [`dung-may-chu-kich-hoat-full.md`](dung-may-chu-kich-hoat-full.md); URL và khóa công khai trong repo đổi sau khi dựng xong.
2. Bản Full phát hành ở Releases của repo Full hay nơi khác.
3. Bản Full thay thế bản gốc trên máy người dùng (giữ `AppId`, thư mục) hay cài song song (đổi tên, `AppId`, lệnh).
   **Đã chốt 12/11/2026:** thay thế như nâng cấp, có cảnh báo (mục 3).
