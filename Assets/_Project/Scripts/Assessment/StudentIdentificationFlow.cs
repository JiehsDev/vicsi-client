// Assets/_Project/Scripts/Assessment/StudentIdentificationFlow.cs
using System.Threading;
using System.Threading.Tasks;

public enum IdentificationOutcome
{
    Identified,
    InvalidFormat,          // not 8 symbols from the code alphabet
    NotRecognized,          // server said invalid, or the cached directory doesn't know it
    Disabled,               // the cached directory knows this code and it is disabled
    DirectoryNeverSynced,   // offline AND this headset has never downloaded the class list
    TestModeUnavailable,    // Continue Without Code, but no test student is known yet
}

public class IdentificationAttempt
{
    public IdentificationOutcome Outcome;
    public IdentifiedStudent Student;   // set when Identified
}

/// <summary>
/// The decision logic behind the pre-session identification screen - no UI, so it is
/// testable without a scene. A student code is checked ONLINE first (the server is
/// authoritative, e.g. for a code regenerated since the last sync); only if the server
/// can't be reached does it fall back to the cached directory, which is what makes a
/// previously synced code work offline. Continue Without Code resolves the EXISTING test
/// student from the synced directory - never a made-up id.
///
/// Everything resolves to a stable studentId; nothing here reads or stores a pairing
/// token, an upload token or the device key.
/// </summary>
public class StudentIdentificationFlow
{
    private readonly IVrCodeValidator validator;
    private readonly System.Func<StudentDirectoryCache> cache;
    private readonly System.Func<float> validationTimeoutSeconds;

    public StudentIdentificationFlow(IVrCodeValidator validator, System.Func<StudentDirectoryCache> cache, System.Func<float> validationTimeoutSeconds)
    {
        this.validator = validator;
        this.cache = cache;
        this.validationTimeoutSeconds = validationTimeoutSeconds;
    }

    public async Task<IdentificationAttempt> IdentifyByCodeAsync(string typedCode, CancellationToken cancellationToken = default)
    {
        string canonical = StudentAccessCodes.Normalize(typedCode);
        if (canonical == null)
        {
            return new IdentificationAttempt { Outcome = IdentificationOutcome.InvalidFormat };
        }

        if (validator != null)
        {
            float timeout = validationTimeoutSeconds?.Invoke() ?? 5f;
            var timeoutTask = Task.Delay(System.TimeSpan.FromSeconds(timeout + 1f));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var validation = validator.ValidateAsync(canonical, timeout, cts.Token);
            if (await Task.WhenAny(validation, timeoutTask) == validation)
            {
                var online = await validation;
                if (online.Outcome == VrCodeValidationOutcome.Valid)
                {
                    var student = new IdentifiedStudent
                    {
                        StudentId = online.Student.studentId,
                        DisplayName = online.Student.displayName.Trim(),
                        Source = "online-code",
                    };
                    if (online.Student.classes != null)
                    {
                        foreach (var c in online.Student.classes)
                        {
                            if (c != null && !string.IsNullOrEmpty(c.classId))
                            {
                                student.Classes.Add(new CachedStudentClass { classId = c.classId, className = c.className, classSection = string.IsNullOrEmpty(c.classSection) ? null : c.classSection });
                            }
                        }
                    }
                    return new IdentificationAttempt { Outcome = IdentificationOutcome.Identified, Student = student };
                }
                if (online.Outcome == VrCodeValidationOutcome.Invalid)
                {
                    // The server is authoritative when reachable - even if this headset's
                    // older cache still knows the code (e.g. regenerated since last sync).
                    return new IdentificationAttempt { Outcome = IdentificationOutcome.NotRecognized };
                }
            }
            else
            {
                cts.Cancel();
            }
        }

        // Offline / server unavailable: the cached directory.
        var directory = cache?.Invoke();
        if (directory == null || directory.IsEmpty)
        {
            return new IdentificationAttempt { Outcome = IdentificationOutcome.DirectoryNeverSynced };
        }
        var lookup = await directory.LookupAsync(canonical);
        switch (lookup.Outcome)
        {
            case StudentCodeLookupOutcome.Valid:
                return new IdentificationAttempt
                {
                    Outcome = IdentificationOutcome.Identified,
                    Student = new IdentifiedStudent
                    {
                        StudentId = lookup.Student.studentId,
                        DisplayName = lookup.Student.displayName,
                        Classes = new System.Collections.Generic.List<CachedStudentClass>(lookup.Student.classes),
                        Source = "offline-code",
                    },
                };
            case StudentCodeLookupOutcome.Disabled:
                return new IdentificationAttempt { Outcome = IdentificationOutcome.Disabled };
            default:
                return new IdentificationAttempt { Outcome = IdentificationOutcome.NotRecognized };
        }
    }

    /// <summary>Continue Without Code: the existing test student from the synced directory, flagged as a test session.</summary>
    public IdentificationAttempt ContinueWithoutCode()
    {
        var test = cache?.Invoke()?.TestStudent;
        if (test == null || !test.IsKnown)
        {
            return new IdentificationAttempt { Outcome = IdentificationOutcome.TestModeUnavailable };
        }
        return new IdentificationAttempt
        {
            Outcome = IdentificationOutcome.Identified,
            Student = new IdentifiedStudent
            {
                StudentId = test.studentId,
                DisplayName = test.displayName,
                Classes = new System.Collections.Generic.List<CachedStudentClass>(test.classes),
                IsTestSession = true,
                Source = "test-mode",
            },
        };
    }
}
