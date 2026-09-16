# ADR-003 — Security and target model

Status: Accepted for v0.1.

## Trust model

Untrusted inputs include ScenarioSpec, ProviderContract, fixtures, SUT requests/responses, webhook responses and DNS. Use synthetic payment data only. Paytness is not a DLP product or PCI scanner/certifier.

## Network defaults

Provider listens on `127.0.0.1:8787` by default. Non-loopback listen requires explicit operator opt-in. SUT origin comes from `--sut`; scenario files contain relative paths only.

Loopback is allowed. Private targets require exact `--allow-target host:port`. Public targets require both exact allow and `--allow-public-targets`. Hard-deny link-local, multicast, unspecified, cloud-metadata destinations and non-HTTP(S) schemes.

Disable automatic redirects and use normal system TLS validation; there is no insecure TLS switch.

DNS/connect policy must validate addresses at effective connection time using `SocketsHttpHandler.ConnectCallback` or an equivalent runtime primitive, preserving Host/SNI.

## Parsing and resource bounds

- Scenario <= 1 MiB; contract <= 512 KiB; fixture <= 256 KiB; fixtures total <= 8 MiB.
- YAML depth <= 32; nodes/events <= 10,000; reject aliases, anchors, merge keys, custom tags, duplicate keys and remote references.
- provider request/SUT response <= 256 KiB; retained body evidence <= 64 KiB; EvidenceStore <= 64 MiB.
- logical payments 100; requests/attempts/events/deliveries 1,000 each; observations/invariants 500; scheduled actions 2,000.
- timeout 60 s default, 5 min normal hard maximum; shutdown <= 5 s.

Paths are relative to ScenarioRoot and canonicalized after symlink resolution; they must remain inside the root before opening.

Secrets enter only through explicit CLI env mappings. Redaction happens before EvidenceStore. HMAC v0.1 is HMAC-SHA256 over raw webhook body.
