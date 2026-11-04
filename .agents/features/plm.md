# PLM, Formulas, Sample Requests

Router khi sửa PLM dashboard, formulas/manufacturing formulas, sample requests, color chip, message hoặc attachment. Nạp phần liên quan chưa có đủ trong context:

- Formula/material policy, resolve formula, hiển thị item, lookup hoặc keyword Product.ColourCode/SampleRequest.ExternalId/Formula.ExternalId: `plm-formulas.md`, kể cả query Sample Request.
- Sample Request, PATCH/clear, approval, audit, message/thread, attachment hoặc side effect: `plm-sample-requests.md`, kể cả flow gọi từ Formula.
- Dashboard/color chip hoặc PLM khác: README của use case theo chính sách đọc ở root; thêm hai file trên nếu dependency tương ứng chịu tác động.

Security, field visibility, file và notification vẫn theo router root. Task đi qua cả hai flow phải đọc cả hai file con.
