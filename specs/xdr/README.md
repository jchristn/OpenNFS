# XDR Source Corpus

This directory vendors the primary RFC source material and normalized XDR inputs used to build OpenNFS protocol types.

## Purpose

- Keep the protocol corpus under source control.
- Avoid scraping RFC prose from the network at build time.
- Make every normalization decision explicit and auditable.

## Directory Ownership

- `rpc/`
  - Owning output project: `src/OpenNFS.Rpc/`
  - Owning generated namespace: `OpenNFS.Rpc.Generated`
- `nfs3/`
  - Owning output project: `src/OpenNFS.Protocol.V3/`
  - Owning generated namespace: `OpenNFS.Protocol.V3.Generated`
- `nfs4/`
  - Owning output projects:
    - `src/OpenNFS.Protocol.V40/`
    - `src/OpenNFS.Protocol.V41/`
    - `src/OpenNFS.Protocol.V42/`

## Vendored Inputs

### `rpc/`

- `rfc5531.txt`
  - Source: RFC 5531, "RPC: Remote Procedure Call Protocol Specification Version 2", May 2009
  - Original sections used: Section 8.2, Section 9, Appendix A
  - Owning output project: `src/OpenNFS.Rpc/`
  - Normalization: none; raw RFC text is retained as vendored provenance input

- `rpc_msg.x`
  - Source: RFC 5531, Section 8.2, Section 9, Appendix A
  - Owning output project: `src/OpenNFS.Rpc/`
  - Normalization applied:
    - assembled the standalone RPC message and `AUTH_SYS` XDR fragments from multiple RFC sections into one compilable `.x` file
    - preserved RFC-defined numeric values and field order

- `rfc1833.txt`
  - Source: RFC 1833, "Binding Protocols for ONC RPC Version 2", August 1995
  - Original sections used: Section 2.1, Section 3.1
  - Owning output project: `src/OpenNFS.Rpc/`
  - Normalization: none; raw RFC text is retained as vendored provenance input

- `rpcb_prot.x`
  - Source: RFC 1833, Section 2.1
  - Owning output project: `src/OpenNFS.Rpc/`
  - Normalization applied:
    - kept the RFC's published `rp__list` spelling as-is
    - reformatted the RFC block into a standalone `.x` file without changing numeric assignments

- `pmap_prot.x`
  - Source: RFC 1833, Section 3.1
  - Owning output project: `src/OpenNFS.Rpc/`
  - Normalization applied:
    - converted the RFC text's `struct *pmaplist` presentation into a compilable forward `typedef` plus `struct pmaplist` form
    - preserved RPC program, version, and procedure numbers

### `nfs3/`

- `rfc1813.txt`
  - Source: RFC 1813, "NFS Version 3 Protocol Specification", June 1995
  - Original sections used: Section 2, Section 3, Appendix I, Appendix II
  - Owning output project: `src/OpenNFS.Protocol.V3/`
  - Normalization: none; retained as the vendored prose source for `nfs3.x`, `mount3.x`, and the RFC-side NLM v4 delta corpus

- `nfs3.x`
  - Source: RFC 1813, Section 2 and Section 3
  - Owning output project: `src/OpenNFS.Protocol.V3/`
  - Generator entry point: yes
  - Normalization applied:
    - assembled the RFC's constants, common type definitions, per-procedure synopsis declarations, and final program block into one standalone compilable `.x` file
    - corrected the RFC synopsis typo `FSINFOargs` to `FSINFO3args` so the type name matches the published procedure signature
    - normalized the recursive `entry3` and `entryplus3` pointer fields into forward typedef forms for standalone compilation without changing the wire shape

- `mount3.x`
  - Source: RFC 1813, Appendix I
  - Owning output project: `src/OpenNFS.Protocol.V3/`
  - Generator entry point: yes
  - Normalization applied:
    - corrected the published `case MNT_OK` union arm to `case MNT3_OK` so the discriminant matches the `mountstat3` enum defined in the same appendix
    - assembled the appendix's basic types, linked-list typedefs, and program block into a standalone compilable `.x` file

- `nlm3.x`
  - Source: The Open Group Technical Standard, "Protocols for Interworking: XNFS, Version 3W", Chapter 10, "Network Lock Manager Protocol"
  - Owning output project: `src/OpenNFS.Protocol.V3/`
  - Normalization applied:
    - collapsed the chapter's `LM_MAXNAMELEN = LM_MAXSTRLEN + 1` expression into the standalone literal `1025`
    - normalized Sun RPC shorthand declarations such as bare `opaque`, `unsigned`, and `long` fields into explicit standalone XDR forms
    - resolved the chapter's mixed procedure/type presentation into one compilable `.x` file while preserving the published program, version, and procedure numbers
    - retained as a supporting normalization input for NLM v4 composition rather than a declared generator entry point

- `nlm4.x`
  - Source: RFC 1813, Appendix II, plus The Open Group Technical Standard, "Protocols for Interworking: XNFS, Version 3W", Chapter 14
  - Owning output project: `src/OpenNFS.Protocol.V3/`
  - Normalization applied:
    - composed a standalone `.x` file from the standards-defined NLM v4 deltas plus the unchanged NLM v3 declarations they explicitly inherit
    - carried forward unchanged constants and argument/result structures with versioned names because the standards only describe differences, not a standalone compilable block
    - widened lock holder and lock range fields to `uint64`, introduced the RFC-defined `int32`/`uint32` aliases, and preserved the published program, version, and procedure numbers
  - Generator entry point: yes

- `rfc1094.txt`
  - Source: RFC 1094, "NFS: Network File System Protocol specification", March 1989
  - Original sections used: historical NFSv2 text plus Appendix A
  - Owning output project: `src/OpenNFS.Protocol.V3/`
  - Normalization: none; retained as a local historical input

- `nsm.x`
  - Source: The Open Group Technical Standard, "Protocols for Interworking: XNFS, Version 3W", Chapter 11, "Network Status Monitor Protocol"
  - Owning output project: `src/OpenNFS.Protocol.V3/`
  - Generator entry point: yes
  - Normalization applied:
    - normalized the chapter's RPC-language fragments into one standalone `.x` file
    - resolved the chapter's mixed presentation style by making `res` an explicit enum
    - normalized the `stat_chge`/`SM_NOTIFY` naming to the procedure-page form `stat_chge`, which is consistent with the chapter's dedicated `SM_NOTIFY` reference page
  - Important note:
    - RFC 1094 Appendix A is the historical mount protocol, not NSM/statd
    - NSM provenance is therefore taken from the XNFS v3W standard instead of RFC 1094

### `nfs4/`

- `rfc7531.txt`
  - Source: RFC 7531, "Network File System (NFS) Version 4 External Data Representation Standard (XDR) Description", March 2015
  - Original section used: Section 2
  - Owning output project: `src/OpenNFS.Protocol.V40/`
  - Normalization: none; raw RFC text is retained as vendored provenance input

- `nfs4_0.x`
  - Source: RFC 7531, Section 2
  - Owning output project: `src/OpenNFS.Protocol.V40/`
  - Normalization applied:
    - extracted exactly as described by RFC 7531 by removing leading whitespace and the `///` sentinel prefix

- `rfc5662.txt`
  - Source: RFC 5662, "Network File System (NFS) Version 4 Minor Version 1 External Data Representation Standard (XDR) Description", January 2010
  - Original section used: XDR description body
  - Owning output project: `src/OpenNFS.Protocol.V41/`
  - Normalization: none; raw RFC text is retained as vendored provenance input

- `nfs4_1.x`
  - Source: RFC 5662
  - Owning output project: `src/OpenNFS.Protocol.V41/`
  - Normalization applied:
    - extracted from the RFC's sentinel-prefixed XDR block using the RFC's documented line-oriented format

- `rfc7862.txt`
  - Source: RFC 7862, "Network File System (NFS) Version 4 Minor Version 2 Protocol", November 2016
  - Original use: semantic reference for v4.2 behavior
  - Owning output project: `src/OpenNFS.Protocol.V42/`
  - Normalization: none; retained as a local semantic reference alongside the XDR corpus

- `rfc7863.txt`
  - Source: RFC 7863, "Network File System (NFS) Version 4 Minor Version 2 External Data Representation Standard (XDR) Description", November 2016
  - Original section used: Section 2
  - Owning output project: `src/OpenNFS.Protocol.V42/`
  - Normalization: none; raw RFC text is retained as vendored provenance input

- `nfs4_2.x`
  - Source: RFC 7863, Section 2
  - Owning output project: `src/OpenNFS.Protocol.V42/`
  - Normalization applied:
    - extracted exactly as described by RFC 7863 by removing leading whitespace and the `///` sentinel prefix

- `rfc8881.txt`
  - Source: RFC 8881, "Network File System (NFS) Version 4 Minor Version 1 Protocol", August 2020
  - Original use: current semantic reference for NFSv4.1 behavior and later normalization decisions
  - Owning output projects:
    - `src/OpenNFS.Protocol.V41/`
    - `src/OpenNFS.Protocol.V42/`
  - Normalization: none; retained as a local protocol reference

## Current Gaps

- None within Milestone 1.1. The vendored standalone corpus now covers RPC, NFSv3, MOUNT v3, NLM v4, NSM, and NFSv4.0/v4.1/v4.2.
- None within Phase 1. Source acquisition, XDR parsing, code emission, checked-in generated output, and baseline drift verification are now in place.

## Regeneration Entry Point

Use the repository script below to validate the current manifest, parse the explicit generation entry-point files, and regenerate the checked-in output under each owning `Generated/` directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Generate-Xdr.ps1
```
