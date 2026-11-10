using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Services;

/// <summary>
/// VNPay return/IPN callbacks are signed by VNPay and, in practice, do not include vnp_CurrCode.
/// Regression: a strict currency equality made every real callback look "unsigned" (since 2026-11-03).
/// </summary>
static class VnPayCallbackChecks
{
    private const string Secret = "AUDITSECRETAUDITSECRETAUDITSECRET";
    private const string Terminal = "AUDITTMN";

    public static async Task Run(Func<string, Func<Task<bool>>, Task> check)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VnPay:HashSecret"] = Secret, ["VnPay:TmnCode"] = Terminal, ["VnPay:CurrCode"] = "VND"
        }).Build();
        var service = new VnPayService(configuration, new HttpClient(), null!);

        // Exactly the parameter set VNPay sandbox returned for a real successful ATM payment.
        Dictionary<string, string> RealCallback() => new()
        {
            ["vnp_Amount"] = "65000000", ["vnp_BankCode"] = "NCB", ["vnp_BankTranNo"] = "VNP15729816",
            ["vnp_CardType"] = "ATM", ["vnp_OrderInfo"] = "Thanh toan hoa don SC-20261109015837-95f0c533ae9c",
            ["vnp_PayDate"] = "20261109085904", ["vnp_ResponseCode"] = "00", ["vnp_TmnCode"] = Terminal,
            ["vnp_TransactionNo"] = "15729816", ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = "PAY-22d19ebfbc3e4ae09034d14d4246913c"
        };
        Dictionary<string, string> Signed(Dictionary<string, string> data)
        {
            var library = new VnPayLibrary();
            foreach (var (key, value) in data) library.AddRequestData(key, value);
            var url = library.CreateRequestUrl("https://vnpay.example.test/return", Secret);
            return new Dictionary<string, string>(data) { ["vnp_SecureHash"] = url[(url.LastIndexOf('=') + 1)..] };
        }

        await check("VNPay signed success callback without vnp_CurrCode is accepted as paid", () =>
        {
            var result = service.ProcessCallback(Signed(RealCallback()));
            return Task.FromResult(result.IsValidSignature && result.IsSuccess && result.Amount == 650000m
                && result.InvoiceNumber == "PAY-22d19ebfbc3e4ae09034d14d4246913c" && result.TransactionNo == "15729816");
        });

        await check("VNPay callback that does state VND is still accepted", () =>
        {
            var data = RealCallback(); data["vnp_CurrCode"] = "VND";
            var result = service.ProcessCallback(Signed(data));
            return Task.FromResult(result.IsValidSignature && result.IsSuccess);
        });

        await check("VNPay callback in a non-VND currency is never treated as a successful payment", () =>
        {
            var data = RealCallback(); data["vnp_CurrCode"] = "USD";
            var result = service.ProcessCallback(Signed(data));
            return Task.FromResult(!result.IsSuccess);
        });

        await check("VNPay callback with a tampered amount or another terminal is rejected", () =>
        {
            var tampered = Signed(RealCallback()); tampered["vnp_Amount"] = "100";
            var foreignData = RealCallback(); foreignData["vnp_TmnCode"] = "OTHERTMN";
            var foreign = service.ProcessCallback(Signed(foreignData));
            var tamperedResult = service.ProcessCallback(tampered);
            return Task.FromResult(!tamperedResult.IsValidSignature && !tamperedResult.IsSuccess
                && !foreign.IsValidSignature && !foreign.IsSuccess);
        });
    }
}
