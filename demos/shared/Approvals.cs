public sealed class Approver
{
    const string RehearsalFlag = "--auto-approve-for-rehearsal";

    readonly bool _autoApprove;
    readonly TraceLog? _trace;

    Approver(bool autoApprove, TraceLog? trace) =>
        (_autoApprove, _trace) = (autoApprove, trace);

    public static Approver FromArgs(string[] args, TraceLog? trace = null)
    {
        if (!args.Contains(RehearsalFlag)) return new(false, trace);
        if (Environment.GetEnvironmentVariable("HARNESS_REHEARSAL") != "1")
            throw new SetupException($"{RehearsalFlag} is for scripts/rehearse.sh only.");
        return new(true, trace);
    }

    public AIContent Review(ToolApprovalRequestContent request)
    {
        var call = (FunctionCallContent)request.ToolCall;
        Ui.Line(Tone.Warn, $"🔐 approval needed: {Ui.Call(call)}");
        var approved = Ask("   approve? [y/N] ") is "y" or "Y" or "yes";
        if (approved) Ui.Line(Tone.Ok, "   ✔ approved");
        else Ui.Line(Tone.Deny, "   ✖ rejected");
        _trace?.Write("approval", call.Name, new { call.CallId, approved });
        return request.CreateResponse(approved,
            approved ? null : "The operator rejected this call.");
    }

    public bool Confirm(string question)
    {
        Ui.Line(Tone.Warn, $"🔐 {question}");
        var yes = Ask("   [Y/n] ") is "" or "y" or "Y";
        _trace?.Write("approval", question, new { approved = yes });
        return yes;
    }

    string? Ask(string prompt)
    {
        if (!_autoApprove) return Deadline.WhileHumanDecides(() => Ui.Ask(prompt));
        Ui.Line(Tone.Muted, $"{prompt}y (rehearsal auto-approve)");
        return "y";
    }
}
