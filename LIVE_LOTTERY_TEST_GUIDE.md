# Hướng Dẫn Kiểm Thử Luồng Bốc Thăm Trực Tuyến Live (Live Lottery Test Guide)

Tài liệu hướng dẫn chi tiết kịch bản kiểm thử (Test Workflow) cho chức năng **Live Bốc Thăm Trực Tuyến** của hệ thống Quản lý Nhà ở Xã hội (RHS), phù hợp với giao diện **Khu vực 1: Sảnh quay số**, **Khu vực 2: Danh sách trúng tuyển vừa bốc** và **Khu vực 3: Thống kê quỹ căn**.

---

## 1. Tổng Quan Luồng Nghiệp Vụ & Vai Trò (Roles & FSM)

### FSM Trạng Thái Phiên Bốc Thăm
```
[Scheduled] ──> [WaitingLobby] ──> [Live] ⇄ [Paused] ──> [Finished] ──> [Published]
```

### Phân Quyền Vai Trò Trong Phiên Live
| Vai trò | Quyền hạn & Thao tác |
| :--- | :--- |
| **Chủ đầu tư (CĐT)** | Lên lịch bốc thăm, Mở sảnh chờ, Bắt đầu Live, **Bốc tiếp** (`draw-next`), **Pause**, **Resume**, **Kết thúc** phiên. |
| **Sở Xây dựng (SXD)** | Phê duyệt lịch bốc thăm, **Giám sát online trong SignalR Hub** (Bắt buộc phải có ≥ 1 cán bộ SXD online để CĐT chạy Live), Công bố kết quả phiên (`Publish`). |
| **Người dân (Applicant)** | Nhập mã OTP (`JoinCode`) vào sảnh, xem màn hình Live quay số và nhận kết quả cập nhật thời gian thực. |

---

## 2. Chuẩn Bị Môi Trường & Dữ Liệu Test (Prerequisites)

1. **Khởi chạy hệ thống Backend (API & SignalR Hub)**:
   - Base URL REST API: `https://localhost:7143/api` (hoặc `http://localhost:5068/api`).
   - SignalR Hub URL: `https://localhost:7143/hubs/lottery`.
2. **Chuẩn bị 3 Tài khoản & Bearer Token**:
   - `Token_CDT`: Tài khoản Chủ đầu tư (Role: `HousingDeveloper`).
   - `Token_SXD`: Tài khoản Sở Xây dựng (Role: `DepartmentOfConstruction`).
   - `Token_Applicant`: Tài khoản Người dân có hồ sơ `APPROVED` thuộc dự án (Role: `Applicant`).
3. **Dự án bốc thăm (`ProjectId`)**:
   - Dự án có căn hộ khả dụng (`AvailableUnits > 0`) và có ≥ 2 hồ sơ đăng ký trạng thái `APPROVED` / `APPROVED_BY_TIMEOUT`.

---

## 3. Kịch Bản Kiểm Thử Chi Tiết Theo Các Bước (Step-by-step Test Scenarios)

### Bước 1: CĐT Lên Lịch & SXD Phê Duyệt Lịch Bốc Thăm

#### 1.1 CĐT Lên lịch bốc thăm
- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/schedule`
- **Headers**: `Authorization: Bearer {Token_CDT}`
- **Body JSON**:
  ```json
  {
    "lotteryDate": "2026-08-20T09:00:00Z",
    "lotteryLocation": "Phòng họp trực tuyến Zoom / Sảnh bốc thăm",
    "lotteryType": "ONLINE",
    "lotteryDescription": "Phiên bốc thăm quyền mua nhà ở xã hội đợt 1",
    "totalUnits": 100
  }
  ```
- **Kỳ vọng**: Trả về `200 OK`, `sessionStatus = "Scheduled"`.

#### 1.2 Sở Xây dựng Phê duyệt & Công bố Lịch
- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/schedule/approve`
- **Headers**: `Authorization: Bearer {Token_SXD}`
- **Kỳ vọng**: Trả về `200 OK`, `isLotteryApproved = true`, sinh ra mã `joinCode` OTP 6 số (ví dụ: `"852914"`).

---

### Bước 2: CĐT Mở Sảnh Chờ & Kết Nối SignalR Hub

#### 2.1 CĐT Mở sảnh chờ (WaitingLobby)
- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/session/open-lobby`
- **Headers**: `Authorization: Bearer {Token_CDT}`
- **Kỳ vọng**: Trả về `200 OK`, `sessionStatus = "WaitingLobby"`.

#### 2.2 Kết nối SignalR Hub (`/hubs/lottery`)
- Kết nối SignalR từ Client (CĐT, SXD, Người dân):
  ```javascript
  const connection = new signalR.HubConnectionBuilder()
      .withUrl("https://localhost:7143/hubs/lottery", {
          accessTokenFactory: () => Token_SXD // hoặc Token_Applicant
      })
      .build();
  await connection.start();
  ```
- **Gửi sự kiện Tham gia sảnh**:
  ```javascript
  // CĐT & SXD không cần joinCode. Applicant truyền joinCode OTP ở bước 1.2
  await connection.invoke("JoinProjectLobby", projectId, "852914");
  ```
- **Lắng nghe sự kiện SignalR**:
  ```javascript
  connection.on("ReceiveLiveState", (state) => console.log("Live State:", state));
  connection.on("ReceiveDrawResult", (result) => console.log("Draw Result:", result));
  connection.on("ReceiveLotteryStatus", (status) => console.log("Status Change:", status));
  connection.on("ReceiveSxdSupervisorCount", (count) => console.log("SXD Online:", count));
  connection.on("ReceiveLobbyCount", (count) => console.log("Lobby Count:", count));
  ```
- **Kỳ vọng**:
  - Tải thành công `ReceiveLiveState` đẩy về client.
  - `ReceiveSxdSupervisorCount` ghi nhận số đại diện SXD online (`≥ 1`).

---

### Bước 3: CĐT Bắt Đầu Phiên Live (`StartLive`)

- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/session/start`
- **Headers**: `Authorization: Bearer {Token_CDT}`
- **Điều kiện**: Bắt buộc phải có ≥ 1 tài khoản SXD đang kết nối Hub. Nếu chưa có SXD online, API sẽ báo lỗi: `"Không thể bắt đầu Live: cần ít nhất 1 đại diện Sở Xây dựng đang online giám sát"`.
- **Kỳ vọng**: Trả về `200 OK`, `sessionStatus = "Live"`. Tất cả client trong Hub nhận sự kiện `ReceiveLotteryStatus("Live")`.

---

### Bước 4: Kiểm Tra API Lấy Trạng Thái Màn Hình Live (`GetLiveState`)

- **HTTP Method**: `GET`
- **URL**: `/api/projects/{projectId}/lottery/live-state`
- **Headers**: `Authorization: Bearer {Token_CDT}` (hoặc `{Token_Applicant}`)
- **Kỳ vọng (200 OK)**: Trả về đầy đủ dữ liệu cho 3 khu vực giao diện:
  ```json
  {
    "projectId": "...",
    "projectName": "Dự án NOXH Bình Minh",
    "developerName": "Lê Nguyễn Group",
    "sessionStatus": "Live",
    "totalUnits": 100,
    "drawnUnitsCount": 0,
    "remainingUnits": 100,
    "totalEligibleParticipants": 120,
    "sxdOnlineCount": 1,
    "lobbyCount": 5,
    "priorityWinnersCount": 0,
    "randomWinnersCount": 0,
    "undrawnParticipantsCount": 120,
    "winRatePercentage": 83.3,
    "nextCandidate": {
      "applicationId": "...",
      "applicationCode": "HS-2026-78",
      "applicantName": "Nguyễn Văn A",
      "citizenId": "069123456331",
      "priorityGroup": "GROUP_1"
    },
    "latestDrawResult": null,
    "recentWinners": [],
    "projectApartmentFundStat": {
      "categoryName": "Quỹ căn dự án",
      "totalUnits": 100,
      "remainingUnits": 100,
      "assignedUnits": 0,
      "remainingPercentage": 100.0
    },
    "apartmentFundStats": [...]
  }
  ```

---

### Bước 5: CĐT Thực Hiện Bốc 1 Lượt Tiếp Theo ("Bốc tiếp")

- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/draw-next`
- **Headers**: `Authorization: Bearer {Token_CDT}`
*(Lưu ý: CĐT cũng có thể gọi qua SignalR Hub bằng: `connection.invoke("DrawNextTurn", projectId)`)*

- **Kỳ vọng (200 OK)**:
  1. Trả về thông tin lượt trúng vừa bốc `LiveDrawResultDto`:
     ```json
     {
       "projectId": "...",
       "applicationId": "...",
       "applicationCode": "HS-2026-78",
       "applicantName": "Vũ Thị E",
       "citizenId": "069123456331",
       "maskedCitizenId": "069******331",
       "stt": 1,
       "result": "PRIORITY_WON",
       "slotCode": "A-12.05",
       "drawnAt": "2026-08-14T15:35:22Z",
       "remainingUnits": 99,
       "priorityGroup": "GROUP_1"
     }
     ```
  2. SignalR Hub phát ngay 2 sự kiện tới tất cả client:
     - `ReceiveDrawResult`: Gói tin trúng tuyển của lượt vừa bốc.
     - `ReceiveLiveState`: Trạng thái mới nhất (Tiến độ `1 / 100 Căn`, Căn còn lại `99/100`, % Quỹ căn `99.0%`, Danh sách trúng tuyển vừa bốc được thêm 1 dòng).
  3. Căn hộ `A-12.05` đổi trạng thái thành `ASSIGNED`, ứng viên `Vũ Thị E` đổi trạng thái thành `CONTRACT_PENDING` và được loại ra khỏi danh sách bốc lần sau.

---

### Bước 6: CĐT Tạm Dừng ("Pause") & Tiếp Tục ("Resume")

#### 6.1 Tạm dừng bốc thăm (Pause)
- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/session/pause`
- **Headers**: `Authorization: Bearer {Token_CDT}`
- **Kỳ vọng**: `sessionStatus = "Paused"`. Mọi lệnh `draw-next` trong lúc Paused sẽ bị từ chối với lỗi: `"Chưa tới lúc bốc thăm. Trạng thái phiên hiện tại: Paused"`.

#### 6.2 Tiếp tục bốc thăm (Resume)
- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/session/resume`
- **Headers**: `Authorization: Bearer {Token_CDT}`
- **Kỳ vọng**: `sessionStatus = "Live"`. Phiên tiếp tục cho phép bấm "Bốc tiếp".

---

### Bước 7: CĐT Kết Thúc Phiên Quay Số (`FinishSession`)

- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/session/finish`
- **Headers**: `Authorization: Bearer {Token_CDT}`
- **Kỳ vọng**:
  - `sessionStatus = "Finished"`.
  - Toàn bộ hồ sơ đủ điều kiện còn lại chưa bốc được đánh dấu `LOTTERY_LOST` (Trượt bốc thăm).
  - Bản ghi tổng kết `LotteryDraw` được tạo trong cơ sở dữ liệu.

---

### Bước 8: Sở Xây dựng Công Bố Kết Quả (`PublishSession`)

- **HTTP Method**: `POST`
- **URL**: `/api/projects/{projectId}/lottery/session/publish`
- **Headers**: `Authorization: Bearer {Token_SXD}`
- **Kỳ vọng**:
  - `sessionStatus = "Published"`.
  - Hệ thống tự động gửi thông báo (In-App Notification) đến toàn bộ ứng viên tham dự.

---

### Bước 9: Tải Biên Bản PDF Phiên Bốc Thăm (`DownloadMinutes`)

- **HTTP Method**: `GET`
- **URL**: `/api/projects/{projectId}/lottery/minutes.pdf`
- **Headers**: `Authorization: Bearer {Token_CDT}` (hoặc `{Token_SXD}`)
- **Kỳ vọng**: Trả về file PDF (`application/pdf`) tên `BienBan_BocTham_{projectId}.pdf` chứa đầy đủ danh sách trúng tuyển, thông tin dự án và chữ ký xác nhận của đại diện Sở Xây dựng & CĐT.

---

## 4. Danh Sách Tóm Tắt APIs & SignalR Hub Events

### REST APIs Endpoint Summary
| STT | Endpoint | Method | Role | Chức năng |
| :---: | :--- | :---: | :---: | :--- |
| 1 | `/api/projects/{projectId}/lottery/schedule` | POST | CĐT | Đề xuất lịch bốc thăm |
| 2 | `/api/projects/{projectId}/lottery/schedule/approve` | POST | Sở/Admin | Phê duyệt lịch & phát mã OTP `JoinCode` |
| 3 | `/api/projects/{projectId}/lottery/session/open-lobby` | POST | CĐT | Mở sảnh chờ (`WaitingLobby`) |
| 4 | `/api/projects/{projectId}/lottery/session/start` | POST | CĐT | Bắt đầu bốc thăm (`Live`) - Cần SXD online |
| 5 | `/api/projects/{projectId}/lottery/live-state` | GET | All | Lấy toàn bộ trạng thái màn hình Live |
| 6 | `/api/projects/{projectId}/lottery/draw-next` | POST | CĐT | **Bốc tiếp 1 lượt** (Bốc ngẫu nhiên & gán căn) |
| 7 | `/api/projects/{projectId}/lottery/session/pause` | POST | CĐT | Tạm dừng phiên bốc thăm (`Paused`) |
| 8 | `/api/projects/{projectId}/lottery/session/resume` | POST | CĐT | Tiếp tục phiên bốc thăm (`Live`) |
| 9 | `/api/projects/{projectId}/lottery/session/finish` | POST | CĐT | Kết thúc phiên (`Finished`) & chốt hồ sơ trượt |
| 10 | `/api/projects/{projectId}/lottery/session/publish` | POST | Sở/Admin | Công bố chính thức kết quả (`Published`) |
| 11 | `/api/projects/{projectId}/lottery/minutes.pdf` | GET | CĐT/Sở/Admin | Tải biên bản bốc thăm dạng PDF |

### SignalR Hub Methods & Events (`/hubs/lottery`)
| Loại | Tên Method / Event | Vai trò | Mô tả |
| :--- | :--- | :--- | :--- |
| **Client Invoke** | `JoinProjectLobby(projectId, joinCode)` | All | Tham gia sảnh dự án |
| **Client Invoke** | `DrawNextTurn(projectId)` | CĐT | Kích hoạt Bốc tiếp từ Hub |
| **Client Invoke** | `PauseLive(projectId)` | CĐT | Kích hoạt Pause từ Hub |
| **Client Invoke** | `ResumeLive(projectId)` | CĐT | Kích hoạt Resume từ Hub |
| **Server Broadcast** | `ReceiveLiveState(state)` | Clients | Đẩy toàn bộ dữ liệu trạng tháiLive |
| **Server Broadcast** | `ReceiveDrawResult(result)` | Clients | Đẩy kết quả lượt vừa bốc thăm |
| **Server Broadcast** | `ReceiveLotteryStatus(status)` | Clients | Đẩy cập nhật trạng thái phiên |
| **Server Broadcast** | `ReceiveSxdSupervisorCount(count)`| Clients | Đẩy số đại diện Sở Xây dựng online |
| **Server Broadcast** | `ReceiveLobbyCount(count)` | Clients | Đẩy số người dùng online ở sảnh |

---

## 5. Bảng Kiểm Thử (Test Checklist Matrix)

- [ ] **TC-01**: CĐT tạo lịch bốc thăm thành công.
- [ ] **TC-02**: SXD duyệt lịch & tạo mã OTP 6 số thành công.
- [ ] **TC-03**: CĐT mở sảnh chờ `WaitingLobby` thành công.
- [ ] **TC-04**: SXD kết nối SignalR Hub, `ReceiveSxdSupervisorCount` tăng lên ≥ 1.
- [ ] **TC-05**: CĐT bấm Bắt đầu Live thành công khi đã có SXD online.
- [ ] **TC-06**: Gọi `GET live-state` kiểm tra đúng thông số Quỹ căn (`remainingUnits / totalUnits`) và `winRatePercentage`.
- [ ] **TC-07**: CĐT bấm **Bốc tiếp**, nhận về `LiveDrawResultDto` với mã hồ sơ `HS-2026-XX`, CCCD ẩn `069******331`, mã căn `A-12.05`.
- [ ] **TC-08**: Kiểm tra SignalR broadcast `ReceiveDrawResult` và `ReceiveLiveState` đẩy về đồng thời cho tất cả client đang mở màn hình Live.
- [ ] **TC-09**: Căn hộ đã trúng bị đổi thành `ASSIGNED` và người đã trúng bị loại khỏi danh sách các lượt bốc sau.
- [ ] **TC-10**: Thống kê `projectApartmentFundStat` giảm số căn còn lại và cập nhật % chính xác.
- [ ] **TC-11**: CĐT bấm **Pause**, phiên chuyển sang `Paused`. Bấm **Bốc tiếp** trong lúc Paused nhận thông báo lỗi hợp lệ.
- [ ] **TC-12**: CĐT bấm **Resume**, phiên chuyển về `Live` và tiếp tục bốc thăm bình thường.
- [ ] **TC-13**: CĐT bấm **Kết thúc**, các hồ sơ chưa bốc chuyển sang `LOTTERY_LOST`.
- [ ] **TC-14**: SXD bấm **Publish**, gửi thông báo tới người dân.
- [ ] **TC-15**: Tải biên bản PDF `minutes.pdf` hợp lệ và chứa danh sách người trúng tuyển.
