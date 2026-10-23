// Assets/_Project/Scripts/Instructor/InstructorPinStore.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// The headset-local 4-digit Instructor PIN that guards Instructor / Headset Management.
///
/// STORAGE: &lt;persistentDataPath&gt;/vicsi_instructor_pin.json holds only a random salt
/// and a PBKDF2-HMAC-SHA256 hash of the PIN (never the PIN), plus the failed-attempt
/// counter and cooldown end. Written with the upload queue's crash-safe replace. Local to
/// this headset: never uploaded, never in a session payload, never synced, never logged.
/// A 4-digit PIN is only 10,000 values, so the hash protects against casual reading of
/// the file, not a determined attacker with the file - the cooldown is the real defence
/// against guessing at the headset.
///
/// DEFAULT: until a PIN has been set, the PIN is 0000 (no file needed). Changing it writes
/// the file. A missing or unreadable file means "default" again - the safe recovery for a
/// headset whose PIN was forgotten is to delete the file (e.g. over adb), which only an
/// administrator with device access can do.
///
/// COOLDOWN: after MaxFailedAttempts consecutive wrong PINs, entry is refused for
/// CooldownSeconds. A correct PIN resets the counter. The cooldown end is persisted, so
/// restarting the app doesn't skip it; a device clock moved backwards can't extend it
/// beyond CooldownSeconds from now. There is no permanent lockout.
/// </summary>
public class InstructorPinStore
{
    public const string FileName = "vicsi_instructor_pin.json";
    public const string DefaultPin = "0000";
    private const int Iterations = 20_000;

    public int MaxFailedAttempts { get; }
    public float CooldownSeconds { get; }
    public string FilePath { get; }

    private readonly Func<DateTime> clock;

    [Serializable]
    private class PinFile
    {
        public int version = 1;
        public string saltBase64;
        public string hashBase64;
        public int failedAttempts;
        public string lockedUntilUtc;
    }

    public enum VerifyOutcome { Accepted, Rejected, CoolingDown, Malformed }

    public class VerifyResult
    {
        public VerifyOutcome Outcome;
        public int SecondsRemaining;     // when CoolingDown
        public int AttemptsBeforeCooldown;
    }

    public enum ChangeOutcome { Changed, CurrentPinWrong, NewPinInvalid, ConfirmationMismatch, CoolingDown }

    public InstructorPinStore(string rootDirectory = null, int maxFailedAttempts = 5, float cooldownSeconds = 30f, Func<DateTime> utcClock = null)
    {
        FilePath = Path.Combine(string.IsNullOrEmpty(rootDirectory) ? Application.persistentDataPath : rootDirectory, FileName);
        MaxFailedAttempts = Mathf.Max(1, maxFailedAttempts);
        CooldownSeconds = Mathf.Max(1f, cooldownSeconds);
        clock = utcClock ?? (() => DateTime.UtcNow);
    }

    public static bool IsWellFormed(string pin) => pin != null && pin.Length == 4 && pin.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').Length == 0;

    /// <summary>True while the default PIN (0000) is still in effect.</summary>
    // IsNullOrEmpty, not == null: JsonUtility writes a null string back as "", so a file
    // created only by failed attempts (no PIN set) must still read as "default".
    public bool IsDefaultPin => string.IsNullOrEmpty(LoadOrNull()?.hashBase64);

    /// <summary>Seconds left on an active cooldown (0 = none).</summary>
    public int CooldownSecondsRemaining
    {
        get
        {
            var file = LoadOrNull();
            return file == null ? 0 : Remaining(file);
        }
    }

    public VerifyResult Verify(string pin)
    {
        var file = LoadOrNull() ?? new PinFile();
        int remaining = Remaining(file);
        if (remaining > 0)
        {
            return new VerifyResult { Outcome = VerifyOutcome.CoolingDown, SecondsRemaining = remaining };
        }
        if (!IsWellFormed(pin))
        {
            return new VerifyResult { Outcome = VerifyOutcome.Malformed, AttemptsBeforeCooldown = MaxFailedAttempts - file.failedAttempts };
        }

        if (Matches(file, pin))
        {
            if (file.failedAttempts != 0 || !string.IsNullOrEmpty(file.lockedUntilUtc))
            {
                file.failedAttempts = 0;
                file.lockedUntilUtc = null;
                Save(file);
            }
            return new VerifyResult { Outcome = VerifyOutcome.Accepted };
        }

        file.failedAttempts++;
        if (file.failedAttempts >= MaxFailedAttempts)
        {
            file.failedAttempts = 0;
            file.lockedUntilUtc = clock().AddSeconds(CooldownSeconds).ToString("O");
            Save(file);
            return new VerifyResult { Outcome = VerifyOutcome.CoolingDown, SecondsRemaining = Mathf.CeilToInt(CooldownSeconds) };
        }
        Save(file);
        return new VerifyResult { Outcome = VerifyOutcome.Rejected, AttemptsBeforeCooldown = MaxFailedAttempts - file.failedAttempts };
    }

    public ChangeOutcome Change(string currentPin, string newPin, string confirmPin)
    {
        var check = Verify(currentPin);
        if (check.Outcome == VerifyOutcome.CoolingDown) return ChangeOutcome.CoolingDown;
        if (check.Outcome != VerifyOutcome.Accepted) return ChangeOutcome.CurrentPinWrong;
        if (!IsWellFormed(newPin)) return ChangeOutcome.NewPinInvalid;
        if (newPin != confirmPin) return ChangeOutcome.ConfirmationMismatch;

        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        var file = new PinFile
        {
            saltBase64 = Convert.ToBase64String(salt),
            hashBase64 = Convert.ToBase64String(Hash(newPin, salt)),
        };
        Save(file);
        return ChangeOutcome.Changed;
    }

    // ------------------------------------------------------------------ internals

    private static bool Matches(PinFile file, string pin)
    {
        if (string.IsNullOrEmpty(file.hashBase64) || string.IsNullOrEmpty(file.saltBase64))
        {
            return pin == DefaultPin;
        }
        byte[] expected = Convert.FromBase64String(file.hashBase64);
        byte[] actual = Hash(pin, Convert.FromBase64String(file.saltBase64));
        if (expected.Length != actual.Length) return false;
        int diff = 0;
        for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];
        return diff == 0;
    }

    private static byte[] Hash(string pin, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(pin), salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }

    private int Remaining(PinFile file)
    {
        if (!AssessmentUploadQueueStore.TryParseUtc(file.lockedUntilUtc, out var until)) return 0;
        double seconds = (until - clock()).TotalSeconds;
        if (seconds <= 0) return 0;
        return Mathf.CeilToInt((float)Math.Min(seconds, CooldownSeconds));   // a clock moved back can't extend it
    }

    private PinFile LoadOrNull()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonUtility.FromJson<PinFile>(File.ReadAllText(FilePath));
        }
        catch (Exception)
        {
            return null;   // unreadable -> default PIN (see class comment)
        }
    }

    private void Save(PinFile file) => AssessmentUploadQueueStore.WriteAtomic(FilePath, JsonUtility.ToJson(file));
}
