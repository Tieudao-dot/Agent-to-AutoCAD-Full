# Skill ClaudeCAD

Mỗi thư mục con là một skill theo chuẩn Agent Skills của Anthropic: file `SKILL.md` có frontmatter

```yaml
---
name: ten-skill            # chữ thường, số, gạch nối; trùng tên thư mục
description: Mô tả KHI NÀO dùng skill (tối đa 1024 ký tự) — Claude dựa vào câu này để quyết định có load skill hay không.
---
```

và thân file là hướng dẫn chi tiết (bảng layer, kích thước chữ, quy trình, ví dụ `batch_execute`). File tham khảo dài có thể tách thành file `.md` khác trong cùng thư mục và nhắc tên file trong `SKILL.md`; Claude đọc chúng bằng tool Read khi cần.

Skill có sẵn:

| Skill | Dùng cho |
| --- | --- |
| `ve-kien-truc` | mặt bằng kiến trúc: trục, cột, tường 220/110, cửa, kích thước, tên phòng |
| `ve-mep` | điện (chiếu sáng, ổ cắm, máng cáp) và cấp thoát nước |
| `ve-ha-tang` | đường, lý trình, cao độ, cống, ranh giới, bảng tọa độ |

## Thêm hoặc sửa skill

1. Tạo thư mục mới trong `skills/` (trong bundle: `%AppData%\Autodesk\ApplicationPlugins\ClaudeCAD.bundle\Contents\skills\`), đặt `SKILL.md` theo mẫu trên.
2. Bấm **New** trong palette (hoặc gõ `CLAUDECHATRESTART`). Sidecar nạp lại toàn bộ skill khi bắt đầu phiên mới — không cần build lại gì.

Mỗi `SKILL.md` nên có mục **"Quy trình chuẩn bị"** nhắc Claude gọi `get_drawing_info` + `list_layers` trước, tạo layer/style còn thiếu theo bảng chuẩn, rồi mới vẽ.

Kiểm tra skill (frontmatter, mục bắt buộc, tên tool nhắc tới có tồn tại): `cd agent && npm test`.
