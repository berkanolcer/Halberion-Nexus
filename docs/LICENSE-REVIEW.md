# License review notes

The repository's `LICENSE` contains the licensor-provided text for the
**Halberion Nexus Source-Available License**. It is a custom source-available license,
not an OSI-approved open-source license. This note is a preparation checklist,
not legal advice or a claim that the license is enforceable.

## Items for independent legal review

1. Confirm the identity and authority of each licensor and the stated
   2023–2026 copyright period.
2. Confirm the definitions and intended reach of "Software" and "Derivative
   Work," especially for bundled and larger works.
3. Review the no-sale / no-fee restrictions alongside third-party components.
   Nexus currently depends on MPL-2.0 components through LibreHardwareMonitor,
   Apache-2.0 HidSharp, MIT-licensed packages, and the upstream notice lists
   LGPL-2.1 PawnIO.Modules code embedded in LibreHardwareMonitorLib. These
   components have their own redistribution terms. Mozilla's MPL guidance says
   additional terms for an executable/larger work must not restrict recipients'
   MPL rights. Section 6 preserves third-party terms; counsel should confirm it
   adequately resolves any interaction with the no-sale clause and bundled
   components' source/redistribution obligations.
4. Decide how contributor copyright and permissions will be handled before
   accepting external pull requests. The supplied text says contributors
   retain rights but the project has not yet adopted a contributor agreement.
5. Review termination, liability, jurisdictional limitations, and required
   notices under the laws relevant to the licensors and users.
6. Confirm how the license should be attached to binary distributions and how
   third-party notices and source links are delivered with a self-contained
   executable.

The current GitHub repository has an initial commit containing the MIT
license. The supplied license expressly says it does not retroactively revoke
rights granted under previous licenses. The repository's existing commit and
history are preserved in this local preparation checkout.
