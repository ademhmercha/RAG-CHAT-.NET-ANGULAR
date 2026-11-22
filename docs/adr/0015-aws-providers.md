# 15. AWS providers selected by configuration

Status: accepted

## Context

Local development uses disk, in-memory channels and Ollama; AWS uses S3, SQS and Bedrock. Business code must not know which.

## Decision

| Concern | Setting | Local | AWS |
|---|---|---|---|
| Files | `Storage:Provider` | `Local` | `S3` (`Storage:S3:*`) |
| Queue | `Queue:Provider` | `Channels` | `Sqs` (`Queue:Sqs:*`) |
| Models | `Ai:Provider` | `Ollama` | `Bedrock` (`Ai:Bedrock:*`, package `AWS.Bedrock.MEAI`) |

- Each provider is registered by a `switch` on the setting; an unknown value fails at startup with the list of supported values. Provider-specific options are validated only when that provider is selected (`IValidateOptions`), with `ValidateOnStart`.
- Credentials come from the default AWS chain (task role, instance profile, environment); no key is ever configured in the application.
- S3: server-side encryption `AES256`, checksum behaviour relaxed to `WHEN_REQUIRED` for S3-compatible stores, optional key prefix and `ServiceUrl`/`ForcePathStyle` for LocalStack or MinIO.
- SQS: long polling (capped at 20 s), receive count mapped to the delivery count, `Abandon` implemented with `ChangeMessageVisibility`. Dead-letter queues are configured on the queue itself.
- Bedrock: chat through the Converse API, embeddings through Titan Text Embeddings V2 with the configured dimension (validated to 256, 512 or 1024).
- The architecture tests forbid AWS and Ollama SDKs in `Domain` and `Application`.

## Consequences

- Moving to AWS is a configuration change plus IAM permissions (`s3:PutObject/GetObject/DeleteObject`, `sqs:SendMessage/ReceiveMessage/DeleteMessage/ChangeMessageVisibility`, `bedrock:InvokeModel/Converse*`).
- Providers are covered by unit tests with mocked SDK clients and by DI selection tests; they are not exercised against real AWS in CI.
