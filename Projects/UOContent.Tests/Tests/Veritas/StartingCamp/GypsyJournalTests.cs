using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Mobiles;
using Server.Veritas.StartingCamp;
using Xunit;

namespace UOContent.Tests.Veritas.StartingCamp;

[Collection("Sequential UOContent Tests")]
public class GypsyJournalTests
{
    [Fact]
    public void TryValidateName_RejectsBadFormatAndTakenNames()
    {
        var previousAlgorithm = AccountSecurity.CurrentAlgorithm;
        AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.Argon2;

        var account = Accounts.GetAccount("gypsy-journal-user") as Account ??
                      new Account("gypsy-journal-user", "gypsy-journal-password");
        PlayerMobile owner = null;
        PlayerMobile newcomer = null;
        try
        {
            owner = new PlayerMobile(World.NewMobile);
            owner.DefaultMobileInit();
            owner.RawName = "Madame Zorya";
            account[0] = owner;

            newcomer = new PlayerMobile(World.NewMobile);
            newcomer.DefaultMobileInit();
            newcomer.RawName = "Newcomer";

            Assert.False(GypsyJournal.TryValidateName("Z", newcomer, out var formatError));
            Assert.NotNull(formatError);
            Assert.False(GypsyJournal.TryValidateName("Lord Zorya", newcomer, out _));

            Assert.False(GypsyJournal.TryValidateName("madame zorya", newcomer, out var takenError));
            Assert.NotNull(takenError);

            Assert.True(GypsyJournal.TryValidateName("Madame Zorya", owner, out _));
            Assert.True(GypsyJournal.TryValidateName("Wandering Soul", newcomer, out var error));
            Assert.Null(error);
        }
        finally
        {
            account[0] = null;
            owner?.Delete();
            newcomer?.Delete();
            account.Delete();
            AccountSecurity.CurrentAlgorithm = previousAlgorithm;
        }
    }
}
