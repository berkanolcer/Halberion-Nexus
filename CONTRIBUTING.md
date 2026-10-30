# Contributing

Halberion Nexus is developed by Azat Berkan Ölçer and Halberion Studio. The
project currently uses the custom source-available license in [`LICENSE`](LICENSE),
not an OSI-approved open-source license. Third-party components retain their
own terms; see [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md).

The license and contributor terms are being prepared for legal review. Before
submitting a contribution, open an issue to agree on how the contribution may
be accepted and distributed. Do not assume a pull request transfers your
copyright or grants rights beyond those you expressly provide.

## Before proposing a change

1. Keep changes focused on the relevant module or shared service.
2. Do not add fabricated hardware readings or claim unsupported Windows
   capabilities work.
3. Keep system and network changes opt-in, clearly described, and reversible
   where possible.
4. Never include credentials, personal IP addresses, device serial numbers,
   or real user logs in code, tests, or screenshots.
5. Build the solution and run the Windows checks described in
   [`docs/VALIDATION.md`](docs/VALIDATION.md).
6. Describe user-visible behavior and the Windows version or hardware needed
   to reproduce a change.

Changes to third-party code or dependencies must preserve their licenses and
update [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md).
