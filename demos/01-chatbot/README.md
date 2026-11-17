# 01 · The chatbot

```bash
dotnet run demos/01-chatbot/chatbot.cs
```

Type a question, press Enter; an empty line exits. Ask the hook question:

> Orders has been returning HTTP 500 on GET /orders/{id}/total since this morning's deploy.
> What is the root cause? One sentence, no caveats.

## What to notice

- Thirteen lines: a file-based app, one `#:include`, `AsAIAgent`, a session and
  `RunStreamingAsync`.
- It answers instantly and confidently, and it is guessing ("a null reference…"). It has no
  logs, no code and no way to check. The real cause is a missing dictionary key.
- The session keeps the conversation, but nothing else is there yet: no tools, no plan,
  no boundaries.

## Workshop exercises

1. Run it with `-e AGENT_MODEL=gpt-5.4` and compare how the bigger model guesses.
2. Change the instructions so it must say "I don't know" when it cannot check. Does that make
   it more useful, or only more honest?
