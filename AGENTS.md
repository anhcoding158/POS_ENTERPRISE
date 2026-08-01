# AGENTS.md — Chỉ dẫn lõi POS Enterprise

## Phạm vi và vị trí

- Áp dụng cho toàn bộ repository `D:\Projects_1\POS_Enterprise_DotNet`.
- Solution: `D:\Projects_1\POS_Enterprise_DotNet\POS.Enterprise.slnx`.
- Instruction file nằm sâu hơn, nếu được tạo có chủ ý trong tương lai, chỉ được bổ sung quy tắc hẹp hơn; không được âm thầm phá quy tắc gốc này.

## Thứ tự nguồn sự thật

Khi có khác biệt, dùng thứ tự ưu tiên:

1. Source hiện tại trong repository.
2. Git status, diff và history hiện tại.
3. Project Memory đã commit.
4. Evidence/log do người dùng cung cấp.
5. Snapshot hoặc Context Pack cũ.

Không lấy snapshot cũ ghi đè source mới và không dùng nội dung chat/snapshot cũ làm replacement mù.

## Kiến trúc dependency lõi

Project references hiện tại xác nhận hướng:

`POS.Domain → POS.Application → POS.Infrastructure → POS.Wpf`

- `POS.Domain` không phụ thuộc EF Core, Infrastructure hoặc WPF.
- `POS.Application` chỉ phụ thuộc production project `POS.Domain`.
- `POS.Infrastructure` triển khai persistence/integration và phụ thuộc Application/Domain.
- `POS.Wpf` là UI/composition root.
- Tests không được làm production projects phụ thuộc ngược vào test assembly.
- Không đặt business logic trong WPF code-behind.
- View/ViewModel không truy cập database trực tiếp.

## Đọc bắt buộc trước mỗi checkpoint

1. `D:\Projects_1\POS_Enterprise_DotNet\AGENTS.md`
2. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`
3. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\BUSINESS-INVARIANTS.md`
4. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\MASTER-ROADMAP.md`
5. `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md`

Trong lúc R0.5 đang xây dựng, nếu tài liệu bắt buộc chưa tồn tại, phải báo đúng tài liệu còn thiếu; không tự bịa nội dung thay thế, không nhảy sang R1, và tiếp tục đúng subcheckpoint R0.5 mà `CURRENT-STATE.md` chỉ ra.

## Quy tắc làm việc

- Luôn đọc đúng file hiện tại trước khi chỉnh sửa. Với file dài, vừa thay đổi nhiều hoặc không chắc phiên bản, đọc lại toàn bộ phần liên quan.
- Kiểm tra Git status/diff trước khi sửa; chỉ làm đúng phạm vi checkpoint.
- Làm theo batch nhỏ khoảng 2–3 file khi phù hợp.
- Không sửa lỗi “tiện tay” ngoài phạm vi và không hoàn nguyên thay đổi không thuộc phạm vi của mình.
- Không tự ý thêm/nâng package, đổi target framework, đổi namespace diện rộng, đổi DI lifetime, transaction boundary, authentication/authorization policy, schema hoặc format toàn repository. Nếu thật sự cần, báo evidence và ảnh hưởng, rồi chờ đúng checkpoint.

## Git safety

Cấm tự chạy:

- `git reset`
- `git restore`
- `git clean`
- `git stash`
- `git checkout -- .`
- `git rebase`
- `git commit --amend`
- `git push --force`

Không stage, commit hoặc push khi checkpoint chưa yêu cầu. Không dùng `git add .` hoặc `git add -A` khi checkpoint có staging scope cụ thể.

## Database và migration

- SQLite và EF Core migration phải forward-only.
- Không sửa migration đã áp làm cơ chế nâng cấp; không sửa ModelSnapshot độc lập với model/migration hợp lệ.
- Không xóa database hoặc migration history để làm test xanh.
- Không chạy database update hoặc đọc dữ liệu database thật khi chưa được phép.
- Migration nguy hiểm phải có backup/compatibility/recovery plan.
- Không đưa database, WAL, SHM, journal, backup hoặc dữ liệu khách hàng vào Context Pack.

## Testing và chất lượng

- Lỗi đã chứng minh phải có regression test phù hợp.
- Thay đổi checkout/payment phải kiểm tra idempotency, restart, concurrency và transaction.
- Trước closeout checkpoint, chạy đúng các gate checkpoint yêu cầu.
- Quy trình chuẩn: `git diff --check` → build → filtered tests nếu có → full tests → Quality Gate không bỏ EF check → manual acceptance.
- Không ghi PASS nếu gate chưa chạy. Nếu kế thừa baseline cũ, ghi rõ đó là accepted baseline và thời điểm/HEAD tương ứng.

## Project Memory

Đọc Project Memory trước mỗi checkpoint. Khi đóng checkpoint, cập nhật tối thiểu:

- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\CURRENT-STATE.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\KNOWN-ISSUES.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\TEST-BASELINE.md`

Nếu có thay đổi tương ứng, cập nhật:

- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\DECISIONS.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\ARCHITECTURE.md`
- `D:\Projects_1\POS_Enterprise_DotNet\docs\project\BUSINESS-INVARIANTS.md`

Tài liệu phải commit cùng checkpoint code để không lệch commit.

## Security và privacy

- Không hard-code hoặc xuất password, token, secret, số tài khoản đầy đủ, dữ liệu khách hàng hoặc dữ liệu production.
- Log và báo cáo phải sanitize thông tin nhạy cảm.
- Không đọc database rows để tạo Project Memory.

## Báo cáo

- Báo đường dẫn file bằng đường dẫn tuyệt đối.
- Báo file tạo/sửa, gate đã chạy, gate chưa chạy, manual test và việc còn lại.
- Không tuyên bố hoàn thành khi chưa có evidence.
