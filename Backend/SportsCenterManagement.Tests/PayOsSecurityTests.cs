using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SportsCenterManagement.BLL.Common.Helpers;
using Xunit;

namespace SportsCenterManagement.Tests;

public sealed class PayOsSecurityTests
{
    [Fact]
    public void PaymentInfoSignatureIncludesNullFieldsAndCompactSortedTransactions()
    {
        using var document = JsonDocument.Parse("""
            {
              "status": "PAID", "orderCode": 34, "amount": 2000,
              "canceledAt": null, "cancellationReason": null,
              "transactions": [{ "reference": "REF-34", "description": "SC34", "amount": 2000 }]
            }
            """);
        const string canonical = "amount=2000&canceledAt=&cancellationReason=&orderCode=34&status=PAID&transactions=[{\"amount\":2000,\"description\":\"SC34\",\"reference\":\"REF-34\"}]";
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("test-checksum"), Encoding.UTF8.GetBytes(canonical)));

        Assert.True(PayOsSecurity.VerifyWebhookSignature(document.RootElement, signature, "test-checksum"));
        Assert.False(PayOsSecurity.VerifyWebhookSignature(document.RootElement, signature, "wrong-checksum"));
    }

    [Fact]
    public void WebhookMatchesOfficialPayOsSignatureVector()
    {
        using var document = JsonDocument.Parse("""
            {
              "orderCode": 123, "amount": 3000, "description": "VQRIO123",
              "accountNumber": "12345678", "reference": "TF230204212323",
              "transactionDateTime": "2023-02-04 18:25:00", "currency": "VND",
              "paymentLinkId": "124c33293c43417ab7879e14c8d9eb18",
              "code": "00", "desc": "Th\u00e0nh c\u00f4ng",
              "counterAccountBankId": "", "counterAccountBankName": "",
              "counterAccountName": "", "counterAccountNumber": "",
              "virtualAccountName": "", "virtualAccountNumber": ""
            }
            """);

        Assert.True(PayOsSecurity.VerifyWebhookSignature(document.RootElement,
            "412e915d2871504ed31be63c8f62a149a4410d34c4c42affc9006ef9917eaa03",
            "1a54716c8f0efb2744fb28b6e38b25da7f67a925d98bc1c18bd8faaecadd7675"));
    }
}
