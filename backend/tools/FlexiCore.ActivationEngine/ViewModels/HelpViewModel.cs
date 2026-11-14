using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record HelpSection(string Heading, string Body);

public sealed class HelpViewModel : PageViewModel
{
    public HelpViewModel(OwnerPaths paths)
        : base("Help", "How the Activation Engine works and what to do when something is missing.", "\uE897")
    {
        Sections = new List<HelpSection>
        {
            new HelpSection("What this app does",
                "The Activation Engine is the owner's tool. It turns a customer's installation request code into a signed license file. "
                + "It works offline and does not need the FlexiCore ERP to be running. Do not install it on customer computers."),
            new HelpSection("Import an installation request",
                "Open Activate Installation. Paste the request code (it starts with FCR1) or open the request file, then press Verify code. "
                + "A good result only means the code is intact and its checksum is correct. It does not prove who the customer is or that they paid. "
                + "Then press Continue to issue license."),
            new HelpSection("Issue and deliver a license",
                "Open Issue License. Enter the customer name, check the dates, grace days and installation limit, choose the signing key and type its passphrase. "
                + "You choose where to save the file. The app signs the license, checks it again against the public key, writes the file without replacing an existing one, "
                + "and only then records it in the issue ledger. Send the .lic file to the customer. The customer imports it in FlexiCore."),
            new HelpSection("Public keys for the customer application",
                "Open Signing Keys, select a key and press Export selected public key, or Export all public keys. The file holds public keys only. "
                + "It is the file the customer application uses to verify licenses. Never send the .p8 file or the passphrase to anyone."),
            new HelpSection("If signing is not available",
                "If the Dashboard says no signing key exists, create one on Signing Keys. If a key shows a problem, its public key file is missing or does not match. "
                + "Issuing stays blocked until that is fixed. The app never creates a key by itself and never uses a test key in place of a real one."),
            new HelpSection("Where data is stored",
                "Owner data folder: " + paths.Root + ". Signing keys: " + paths.KeysDirectory + ". Issue ledger: " + paths.LedgerPath + ". "
                + "Keep this folder outside any git repository and outside cloud-synced folders. Back up the key files and keep the passphrase safe. "
                + "To use a different folder, set the environment variable FLEXICORE_ENGINE_HOME to an absolute path before starting the app."),
            new HelpSection("What this app cannot do",
                "A signed license works offline on the customer's computer, so it cannot be revoked or suspended from here. "
                + "Customer and license lists come from the issue ledger and show dates only. A renewal means issuing a new license with a new license id."),
            new HelpSection("Safe diagnostics",
                "The Settings page shows paths, versions and folder safety checks. It never shows private keys or passphrases, "
                + "so it is safe to copy into a support message."),
        };
    }

    public IReadOnlyList<HelpSection> Sections { get; }
}
