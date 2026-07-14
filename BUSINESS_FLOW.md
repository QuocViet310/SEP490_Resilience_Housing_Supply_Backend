# RHS Backend — Quy trình nghiệp vụ (NOXH mua/bán)

> Cập nhật: 2026-07-13 — soft-delete: CCCD/email chỉ ràng buộc tài khoản Active; xóa TK giải phóng CCCD + hủy hồ sơ mở.

---

## 1. Vai trò

| Vai trò | Code | Việc chính |
|---|---|---|
| Người dân | `Applicant` | eKYC → tạo hồ sơ → upload 2 giấy tờ → nộp → đặt cọc → xem HĐ nguyên tắc / kết quả bốc thăm |
| Chủ đầu tư | `Housing Developer` | Quản lý dự án; tiếp nhận hồ sơ; **dùng AI như trợ lý thẩm định**; gửi danh sách lên SXD |
| Sở Xây dựng | `Department Of Construction` | Duyệt dự án; hậu kiểm hồ sơ (`PENDING_SXD_REVIEW` → `APPROVED`/`REJECTED`) |
| Admin | `System Administrator` | PolicyConfig, tài khoản staff |

**AI (Gemini) = trợ lý thẩm định cho CĐT**, không thay SXD, không chặn người dân nộp hồ sơ.

---

## 2. Giấy tờ bắt buộc (2 loại)

Người dân **phải nộp đủ cả 2** trước khi `SUBMITTED`:

| # | Loại | `DocumentType` | Liên hệ form |
|---|---|---|---|
| 1 | Giấy chứng nhận hộ nghèo / cận nghèo | `POVERTY_HOUSEHOLD_CERTIFICATE` | `URBAN_POOR` / `URBAN_NEAR_POOR` (Đ30.3) |
| 2 | Giấy xác nhận nhà ở | `HOUSING_CONDITION_PROOF` | Xem bảng dưới (Đ29) |

### Giấy xác nhận nhà ở — 1 loại file, 2 trường hợp khai

| Khai form `HousingStatus` | Ý nghĩa | Căn cứ |
|---|---|---|
| `NO_HOUSE` | Chưa có nhà thuộc sở hữu | Đ29.1 |
| `SMALL_HOUSE` | Có nhà nhưng diện tích bình quân **&lt; 15 m²/người** | Đ29.2 (+ `MAX_AREA_PER_PERSON_M2`) |

Không yêu cầu 2 file nhà ở riêng — nội dung giấy khác nhau theo lựa chọn form.

---

## 3. Luồng hồ sơ (Maker–Checker)

```
DRAFT
  → SUBMITTED          (eligibility Đ29–30 trên form; đủ 2 giấy tờ)
  → REVIEWING          (CĐT tiếp nhận)
  → NEED_MORE_DOCUMENTS ↔ REVIEWING
  → PENDING_SXD_REVIEW (CĐT gửi batch lên SXD)
  → APPROVED | REJECTED
  → DEPOSIT_PAID       (sau VNPay thành công)
```

Đóng: `REJECTED`, `CANCELED`, `EXPIRED`, `DEPOSIT_PAID`.

### 3.1 Khi nộp (`SUBMITTED`)

- Rule engine (`EligibilityRuleEngine`): đối tượng nghèo/cận nghèo đô thị + điều kiện nhà ở Đ29.  
  **Không** áp trần thu nhập 15/30 triệu (Đ30.3).
- Đủ 2 `DocumentType` bắt buộc.
- Đ38.1.e: một người chỉ một hồ sơ active (PolicyConfig `ONE_APPLICATION_PER_APPLICANT`).
- Sinh PDF biên nhận.

### 3.2 CĐT (`REVIEWING`)

- Tiếp nhận, yêu cầu bổ sung, từ chối, hoặc gửi lên SXD.
- **AI trợ lý:** CĐT trigger `POST .../documents/{documentId}/verify` (Gemini).  
  Checklist AI nên bám:
  - Đúng loại giấy
  - Khớp CCCD / họ tên / địa chỉ với eKYC
  - Giấy nhà ở khớp `NO_HOUSE` / `SMALL_HOUSE` (+ diện tích nếu có) — **Đ29**
  - Giấy nghèo/cận nghèo khớp đối tượng đã khai — **Đ30.3**
  - Cảnh báo file không đọc được / sai mẫu
- AI **không** quyết định phê duyệt cuối.

### 3.3 SXD (`PENDING_SXD_REVIEW`)

- Duyệt → `APPROVED` hoặc từ chối → `REJECTED`.
- Đ38.1.đ: chặn nếu cùng CCCD đã `APPROVED` / `DEPOSIT_PAID` hồ sơ khác.
- **Hướng A:** `APPROVED` **không** trừ `AvailableUnits`.
- Tacit approval: quá `TACIT_APPROVAL_DAYS` (mặc định 20) → tự `APPROVED` (cùng đối soát CCCD, không trừ suất).

### 3.4 Đặt cọc

- Sau `APPROVED`, người dân thanh toán trong `DEPOSIT_PAYMENT_HOURS` (mặc định 24h).
- Thành công → `DEPOSIT_PAID` + `SlotCode` + `PrincipleAgreement` (HĐ nguyên tắc).
- Quá hạn → `EXPIRED` (**không** hoàn `AvailableUnits` vì chưa từng trừ lúc duyệt).

**Ý nghĩa HĐ nguyên tắc:** cam kết **tham gia phân suất / bốc thăm**, không đồng nghĩa đã được phân căn. HĐ mua bán chính thức chỉ sau khi trúng.

---

## 4. Hướng A — `AvailableUnits` & bốc thăm (Đ38.2)

| Sự kiện | `AvailableUnits` |
|---|---|
| SXD / tacit `APPROVED` | Không đổi |
| Hủy `APPROVED` / `EXPIRED` | Không đổi |
| Đặt cọc `DEPOSIT_PAID` | Không đổi |
| Bốc thăm: mỗi `WON` / `PRIORITY_WON` | **−1** |
| Bốc thăm: `LOST` | Không đổi |
| Re-run lottery | Hoàn suất người từng trúng → phân bổ lại → trừ lại |

- Trần phân bổ mặc định = `AvailableUnits` còn lại (**không** mặc định = số người đặt cọc).
- Đặt cọc = điều kiện **vào pool** bốc thăm.
- Ưu tiên theo tỷ lệ hồ sơ (Đ38.2); phần còn lại random.

### Đ44 — Công bố

Chỉ hồ sơ: `DEPOSIT_PAID` + có HĐ nguyên tắc + `LotteryResult` ∈ {`WON`, `PRIORITY_WON`}.  
`LOST` không vào danh sách công bố.

---

## 5. Luồng dự án + worker ngầm

| Worker / sự kiện | Hành vi | Policy / căn cứ |
|---|---|---|
| `UPCOMING` → `OPEN` | Khi tới `ApplicationOpenDate` **và** đã công bố đủ ngày từ `PublicAnnounceAt` | `PUBLIC_ANNOUNCE_MIN_DAYS` (Đ38.1.b) |
| `OPEN` → `CLOSED` | Quá `ApplicationCloseDate` | Lịch dự án |
| Tacit approval | `PENDING_SXD_REVIEW` quá hạn → `APPROVED` | `TACIT_APPROVAL_DAYS` |
| Payment timeout | `APPROVED` quá hạn cọc → `EXPIRED` | `DEPOSIT_PAYMENT_HOURS` |

---

## 6. Ba lớp kiểm tra (tránh nhầm vai trò)

```
EligibilityRuleEngine  →  chặn theo form (Đ29–30) khi nộp
AI Gemini              →  đọc PDF, cờ đỏ/xanh cho CĐT (trợ lý)
CĐT / SXD + DB rules   →  quyết định cuối + Đ38.1.đ/e, bốc thăm Đ38.2, Đ44
```

### Việc hệ thống / DB làm — không giao AI

- Đã được hỗ trợ / trùng CCCD — Đ38.1.đ  
- Chỉ một dự án active — Đ38.1.e  
- Công bố / mở–đóng dự án — Đ38.1.b  
- Bốc thăm — Đ38.2  
- Danh sách phân suất — Đ44  

---

## 7. PolicyConfig (tham số nghị định)

| Key | Mặc định (tham chiếu) | Dùng cho |
|---|---|---|
| `TACIT_APPROVAL_DAYS` | 20 | Im lặng đồng ý SXD |
| `DEPOSIT_PAYMENT_HOURS` | 24 | Hết hạn đặt cọc |
| `PUBLIC_ANNOUNCE_MIN_DAYS` | 30 | Chặn mở OPEN sớm |
| `MAX_AREA_PER_PERSON_M2` | 15 | Đ29.2 |
| `ONE_APPLICATION_PER_APPLICANT` | true | Đ38.1.e |

Admin quản lý qua API PolicyConfig (web admin — ngoài phạm vi mobile).

---

## 8. Soft-delete tài khoản & CCCD

| Quy tắc | Chi tiết |
|---|---|
| Xóa TK | `Status = Deleted`; gỡ `CitizenId` / `GoogleId`; đổi email → `deleted+{userId}@…` (giải phóng unique email) |
| eKYC `CheckCitizenId` | Chỉ chặn nếu CCCD thuộc user **`Active`** (khác chính mình) |
| Đăng ký email | `EmailExists` chỉ tính user Active |
| Hồ sơ khi xóa TK | Tự `CANCELED` các hồ sơ mở (DRAFT → APPROVED chưa cọc). Giữ `DEPOSIT_PAID` (Đ38.1.đ) |
| Nộp hồ sơ cùng dự án | `CitizenIdExistsInProject` bỏ EXPIRED; bỏ hồ sơ của user Deleted trừ khi đã `DEPOSIT_PAID` |

---

## 9. Sơ đồ tóm tắt data đổi

| Sự kiện | Status | AvailableUnits | Bản ghi nổi bật |
|---|---|---|---|
| Nộp | → `SUBMITTED` | — | EligibilityAssessment, History, Receipt |
| CĐT nhận / bổ sung / từ chối | `REVIEWING` / … / `REJECTED` | — | History |
| Gửi SXD | → `PENDING_SXD_REVIEW` | — | History |
| SXD / tacit duyệt | → `APPROVED` | **không đổi** | History |
| Hết hạn cọc | → `EXPIRED` | **không đổi** | History |
| Cọc OK | → `DEPOSIT_PAID` | **không đổi** | Payment, SlotCode, PrincipleAgreement |
| Bốc thăm trúng | + `WON`/`PRIORITY_WON` | **−N** | LotteryDraw |
| Bốc thăm trượt | + `LOST` | không đổi | LotteryDraw |

---

## 10. File liên quan (code)

| Module | File chính |
|---|---|
| Eligibility | `EligibilityRuleEngine.cs`, `PriorityGroupConstants`, `HousingStatusConstants` |
| Documents | `DocumentTypeConstants`, `DocumentService.cs` |
| AI | `GeminiDocumentVerificationService.cs`, `DocumentsController` (`POST .../verify`) |
| Review / tacit | `ReviewService.cs`, `ProjectAutomationWorker.cs` |
| Payment / HĐ | `PaymentService.cs`, `PdfContractService.cs`, `PaymentTimeoutWorker.cs` |
| Lottery / Đ44 | `LotteryService.cs`, `BeneficiaryPublishService.cs` |
| Policy | `PolicyService.cs`, `PolicyKeys.cs` |
