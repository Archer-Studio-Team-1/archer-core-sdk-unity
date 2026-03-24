using NUnit.Framework;

namespace ArcherStudio.SDK.IAP.Tests {

    [TestFixture]
    public class ProductInfoTests {

        [Test]
        public void Constructor_SetsAllFields_Correctly() {
            var info = new ProductInfo(
                productId: "com.game.gems100",
                localizedTitle: "100 Gems",
                localizedDescription: "Buy 100 gems for your adventure",
                localizedPrice: "$0.99",
                priceDecimal: 0.99m,
                currencyCode: "USD",
                type: ProductType.Consumable
            );

            Assert.AreEqual("com.game.gems100", info.ProductId);
            Assert.AreEqual("100 Gems", info.LocalizedTitle);
            Assert.AreEqual("Buy 100 gems for your adventure", info.LocalizedDescription);
            Assert.AreEqual("$0.99", info.LocalizedPrice);
            Assert.AreEqual(0.99m, info.PriceDecimal);
            Assert.AreEqual("USD", info.CurrencyCode);
            Assert.AreEqual(ProductType.Consumable, info.Type);
        }

        [Test]
        public void Constructor_NonConsumableType_SetsCorrectly() {
            var info = new ProductInfo(
                "com.game.noads", "No Ads", "Remove all ads", "$2.99",
                2.99m, "USD", ProductType.NonConsumable
            );

            Assert.AreEqual(ProductType.NonConsumable, info.Type);
            Assert.AreEqual("com.game.noads", info.ProductId);
        }

        [Test]
        public void Constructor_SubscriptionType_SetsCorrectly() {
            var info = new ProductInfo(
                "com.game.vip_monthly", "VIP Monthly", "Monthly VIP access", "$4.99",
                4.99m, "EUR", ProductType.Subscription
            );

            Assert.AreEqual(ProductType.Subscription, info.Type);
            Assert.AreEqual("EUR", info.CurrencyCode);
        }

        [Test]
        public void TwoInstances_AreIndependent() {
            var a = new ProductInfo("id_a", "Title A", "Desc A", "$1", 1m, "USD", ProductType.Consumable);
            var b = new ProductInfo("id_b", "Title B", "Desc B", "$2", 2m, "EUR", ProductType.NonConsumable);

            Assert.AreEqual("id_a", a.ProductId);
            Assert.AreEqual("id_b", b.ProductId);
            Assert.AreEqual(1m, a.PriceDecimal);
            Assert.AreEqual(2m, b.PriceDecimal);
        }

        [Test]
        public void DefaultStruct_HasNullStringsAndDefaults() {
            var info = default(ProductInfo);

            Assert.IsNull(info.ProductId);
            Assert.IsNull(info.LocalizedTitle);
            Assert.IsNull(info.LocalizedDescription);
            Assert.IsNull(info.LocalizedPrice);
            Assert.IsNull(info.CurrencyCode);
            Assert.AreEqual(0m, info.PriceDecimal);
            Assert.AreEqual(ProductType.Consumable, info.Type);
        }
    }

    [TestFixture]
    public class PurchaseResultTests {

        [Test]
        public void Succeeded_SetsSuccessAndTransactionData() {
            var result = PurchaseResult.Succeeded(
                "com.game.gems100", "txn_abc123", "receipt_data_xyz"
            );

            Assert.IsTrue(result.Success);
            Assert.AreEqual("com.game.gems100", result.ProductId);
            Assert.AreEqual("txn_abc123", result.TransactionId);
            Assert.AreEqual("receipt_data_xyz", result.Receipt);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Succeeded_HasDefaultFailureReason() {
            var result = PurchaseResult.Succeeded("prod_01", "txn_01", "receipt_01");

            Assert.AreEqual(default(PurchaseFailureReason), result.FailureReason);
        }

        [Test]
        public void Failed_SetsErrorAndReason() {
            var result = PurchaseResult.Failed(
                "com.game.gems100", "Payment declined", PurchaseFailureReason.PaymentDeclined
            );

            Assert.IsFalse(result.Success);
            Assert.AreEqual("com.game.gems100", result.ProductId);
            Assert.AreEqual("Payment declined", result.ErrorMessage);
            Assert.AreEqual(PurchaseFailureReason.PaymentDeclined, result.FailureReason);
        }

        [Test]
        public void Failed_HasNullTransactionIdAndReceipt() {
            var result = PurchaseResult.Failed(
                "prod_01", "Cancelled", PurchaseFailureReason.UserCancelled
            );

            Assert.IsNull(result.TransactionId);
            Assert.IsNull(result.Receipt);
        }

        [Test]
        public void Failed_AllReasons_SetCorrectly(
            [Values(
                PurchaseFailureReason.Unknown,
                PurchaseFailureReason.UserCancelled,
                PurchaseFailureReason.PaymentDeclined,
                PurchaseFailureReason.ProductUnavailable,
                PurchaseFailureReason.PurchasingUnavailable,
                PurchaseFailureReason.ExistingPurchasePending,
                PurchaseFailureReason.DuplicateTransaction,
                PurchaseFailureReason.SignatureInvalid
            )] PurchaseFailureReason reason
        ) {
            var result = PurchaseResult.Failed("prod_01", "error", reason);

            Assert.AreEqual(reason, result.FailureReason);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void TwoResults_AreIndependent() {
            var success = PurchaseResult.Succeeded("prod_a", "txn_a", "receipt_a");
            var failure = PurchaseResult.Failed("prod_b", "error", PurchaseFailureReason.Unknown);

            Assert.IsTrue(success.Success);
            Assert.IsFalse(failure.Success);
            Assert.AreEqual("prod_a", success.ProductId);
            Assert.AreEqual("prod_b", failure.ProductId);
        }
    }

    [TestFixture]
    public class ReceiptValidationResultTests {

        [Test]
        public void ValidResult_SetsFieldsCorrectly() {
            var result = new ReceiptValidationResult(
                isValid: true,
                productId: "com.game.gems100",
                errorMessage: null
            );

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("com.game.gems100", result.ProductId);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void InvalidResult_SetsErrorMessage() {
            var result = new ReceiptValidationResult(
                isValid: false,
                productId: "com.game.gems100",
                errorMessage: "Signature mismatch"
            );

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("com.game.gems100", result.ProductId);
            Assert.AreEqual("Signature mismatch", result.ErrorMessage);
        }

        [Test]
        public void TwoResults_AreIndependent() {
            var valid = new ReceiptValidationResult(true, "prod_a", null);
            var invalid = new ReceiptValidationResult(false, "prod_b", "Bad receipt");

            Assert.IsTrue(valid.IsValid);
            Assert.IsFalse(invalid.IsValid);
            Assert.IsNull(valid.ErrorMessage);
            Assert.AreEqual("Bad receipt", invalid.ErrorMessage);
        }

        [Test]
        public void DefaultStruct_HasFalseIsValidAndNullFields() {
            var result = default(ReceiptValidationResult);

            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.ProductId);
            Assert.IsNull(result.ErrorMessage);
        }
    }
}
