# Bàn giao: Sửa CI đợt H — CI bỏ qua test hỏng, 3 test trợ lý hỏng trên Windows (26/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh`. Nhánh `full/nang-cap-h-sua-ci`, tách từ `full/nang-cap-g-tong-the` @ `547d6af`.
Thread viết H3 sẽ gộp nhánh này cùng `full/nang-cap-h1-test` và `full/nang-cap-h2-test`.

## 1. Chẩn đoán: cả 3 test hỏng là lỗi của test, không phải lỗi sản phẩm

Từ ít nhất bản chốt đợt G (run 45710924398, `# fail 3`), Windows CI có 3 test trợ lý hỏng mà CI vẫn báo xanh.

| Test | Vì sao hỏng trên Windows | Lỗi sản phẩm? |
| --- | --- | --- |
| `nangcapf5.test.ts` › "F5: multileader of the standard" | Test tìm `skills/ve-kien-truc/SKILL.md` bằng `new URL(import.meta.url).pathname`. Trên Windows cách này cho `/D:/a/…`, ghép đường dẫn thành `D:\D:\a\…`, nên báo không có file. | Không. Sản phẩm không dùng cách này. Mô tả lệnh `setup_standards` / `add_mleader` (phần test kiểm) giống hệt nhau trên Windows. |
| `plan-fixes.test.ts` › "KT-42: 3D inserts blocks but sets no attribute values" | Cùng lỗi đường dẫn, khi đọc `plugin/ClaudeCAD.Plugin/Core/Plans.cs`. | Không. Phần kiểm schema `insert_block` của gói 3D vẫn qua trước khi tới dòng đọc file. |
| `nangcapg5.test.ts` › "G5: looks at a traced picture" › "a new user message starts the count again (session)" | Test tìm trong mã nguồn `session.ts` đoạn `void {\n  resetTraceViews();`. Bản checkout trên Windows có xuống dòng CRLF (`\r\n`) nên không khớp. | Không. Code đặt lại số lần xem ảnh ở mỗi tin nhắn mới là một dòng giống nhau trên mọi máy. Test này qua trên Linux, và các test hành vi của bộ đếm (cùng file) qua trên Windows. |

**Ảnh hưởng tới bản 5:** không có. Khách và chủ dự án thử bản 5 (kể cả mục G0-4 về số lần xem ảnh của `trace_image`) không
thấy khác gì: ba lỗi chỉ nằm ở cách test đọc file mẫu.

## 2. Sửa test (không bỏ, không nới)

- F5 và KT-42: dùng `fileURLToPath(import.meta.url)`, cách các test khác trong repo vẫn dùng. Đúng trên cả Linux và Windows.
- G5: regex nhận cả hai kiểu xuống dòng (`\r?\n`). Test vẫn bắt buộc `resetTraceViews()` là dòng đầu của `sendMessage`.

## 3. Sửa workflow `.github/workflows/build.yml`

**Nguyên nhân CI vẫn xanh:** với `shell: pwsh`, GitHub chỉ thêm `exit $LASTEXITCODE` ở cuối step, nên step chỉ đỏ khi lệnh
ngoài **cuối cùng** hỏng. Bước Sidecar chạy `npm ci`, `npm test`, `npm run build`; `npm test` hỏng nhưng `npm run build` qua,
nên bước xanh. Lỗi cmdlet của PowerShell (`Invoke-WebRequest`, `Copy-Item`…) thì vẫn dừng step, vì GitHub đặt
`$ErrorActionPreference = 'stop'`.

**Đã đổi:**

- **Sidecar:** mỗi lệnh `npm ci`, `npm test`, `npm run build` có dòng `if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }` ngay sau.
- **Setup.exe (Inno Setup):** cùng dòng kiểm sau `choco install innosetup` và sau `ISCC.exe`. Trước đây ISCC hỏng thì step vẫn
  đỏ, vì `Copy-Item` không đổi `$LASTEXITCODE`. Nhưng `choco` hỏng mà ISCC còn sót bản cũ thì có thể lọt.

**Đã rà các bước nhiều lệnh còn lại, không phải sửa:**

- **Plugin build:** đã kiểm từng target.
- **All three plugin builds present**, **Version**, **Release notes:** chỉ có cmdlet.
- **Node.js for Windows:** `Invoke-WebRequest` / `Expand-Archive` là cmdlet, hỏng là dừng.
- **Bundle + zip:** một lệnh `node`.
- **Bundle dlls match**, **install.ps1 parses:** đã `exit 1` khi lỗi.

**Không đụng** các bước phát hành: **New version on main?** cố ý bỏ mã lỗi của `gh release view`, cùng **Activation service
configured?** và **Release**.

**Test giữ lỗi không quay lại:** `agent/test/ci-workflow.test.ts` kiểm từng lệnh của hai bước trên có dòng kiểm ngay sau.

**Ảnh hưởng tới nhánh `full/ban-thu-noi-bo`:** nhánh này có bản `build.yml` riêng, cùng lỗi (bước Sidecar và Setup như bản
cũ). Thay đổi ở đây **không** tự sang nhánh đó, vì nhánh đó không bao giờ gộp. Lần build bộ cài thử nội bộ sau nên chép 5 dòng
kiểm này sang, và chạy lại xem `npm test` có xanh thật trên nhánh đó không.

**Kiểm "bước đỏ khi có test hỏng" mà không tốn một lượt CI:**

- Run 45775374409 (H1) đỏ vì `dotnet test` hỏng ở bước một lệnh, nên CI đã dừng đúng ở trường hợp đó.
- Với bước nhiều lệnh, dòng kiểm đặt ngay sau `npm test`: `npm test` trả mã khác 0 thì `exit` trước `npm run build`. Hành vi
  này là của chính PowerShell, không phụ thuộc GitHub.

## 4. Test

- Trợ lý: 409 / 409 qua trên Linux (thêm 2 test workflow).
- Lõi: 1445 / 1445.
- Build 3 target (net48, net8.0-windows, net10.0-windows): qua.

## 5. CI

- Run 45778275884 trên `554307b`, **xanh** (1 lượt). Log trợ lý `# tests 409`, `# pass 409`, `# fail 0`; lõi 1445 / 1445; UI test, build 3 target, bộ cài đều qua. Commit sau chỉ ghi kết quả này, code giữ nguyên như `554307b`.

## 6. Con số chờ chủ dự án duyệt / Quyết định tự chọn

- Không có số mới nào khách thấy.
- Quyết định tự chọn:
  - kiểm `$LASTEXITCODE` sau từng lệnh thay vì bật `$PSNativeCommandUseErrorActionPreference` cho cả job. Bật cho cả job sẽ làm
    hỏng bước "New version on main?", nơi `gh release view` được phép trả lỗi;
  - không sửa `build.yml` của `full/ban-thu-noi-bo`, chỉ ghi lại ở mục 3.
