---
name: axo-to-tco-migration
description: Port (migrate) an AxOpen / AX# vendor component (Axo<Device>, Simatic AX `src/components.<vendor>.<domain>`) into a TcOpen vendor component (Tco<Vendor><Domain> library, TwinCAT 3). Use when the user wants to migrate, port, convert or take over a component from AxOpen / AXOpen / AX# into this repo, or names an `Axo<Device>` class as the source. Builds on the tco-vendor-component skill.
---

# AxOpen -> TcOpen component migration

Source: the AxOpen repository, default `d:\MTS\Develop\Inxton\ax\axopen-templates\axopen` (ask if it is not there).
**The AxOpen repository is read-only.** Never edit, build, format, `apax install` or create files in it; copy files *out* of it only.

Target: a TcOpen vendor component built exactly as described by the **`tco-vendor-component`** skill. Load that skill
first - its scaffold, registration, naming, localization, testing rules and Definition of done apply unchanged. This skill
only adds how to read the AX source and how to translate AX constructs: [references/ax-to-tco-mapping.md](references/ax-to-tco-mapping.md).

## Placeholder names

This skill uses one generic name family, aligned with tco-vendor-component. Replace it consistently with the real names.

| AX placeholder | TcOpen placeholder |
|---|---|
| `components.<vendor>.<domain>` | library `Tco<Vendor><Domain>` = `TcoTemplate` |
| `AxoTemplate` (class in the `<v_X_Y_x>` namespace) | `TcoTemplate[Pn\|Ecat]_v_1_x_x` |
| `AxoTemplate_<Type>` | `TcoTemplate_<Type>_v_1_x_x` (DUTs, tasks, functions) |
| `eAxoTemplate_<Enum>` | `eTcoTemplate_<Enum>_v_1_x_x` |
| `AxoTemplate_BaseTask` | `TcoTemplateTask_v_1_x_x` (ABSTRACT) |
| `FakeTemplate` harness (`ctrl/test/_harness/`) | `TcoTemplateSimulator` in the test PLC |

Real examples are listed only at the end ([Examples](#examples-reference-ports)).

## AX library anatomy

```
src/components.<vendor>.<domain>/
  ctrl/src/AxoTemplate/<v_X_Y_x>/            # component CLASS, Tasks/, TypesStructuresAndEnums/ (or flat)
  ctrl/test/                                 # AxUnit tests, _harness/ (FakeTemplate simulator, S7Harness mocks)
  ctrl/assets/                               # GSDML / ESI / .hwl.yml hardware templates
  src/AXOpen.Components.<Vendor>.<Domain>/   # C# twin partials: messenger text lists (code -> text + help)
      Resources/PlcStringResources*.resx     # translations of every <#...#> PLC text
  src/AXOpen.Components.<Vendor>.<Domain>.blazor/  # views (what the operator sees) + Resources/Strings*.resx
  tests/AXOpen.Components.<Vendor>.<Domain>.Tests/ # xUnit (usually messenger completeness)
  docs/                                      # device docs, wiring, device settings, TROUBLES.md
```

A usage example usually exists in `src/showcase/app/src/components.<vendor>.<domain>/`.

## Names

- No namespaces in TwinCAT: vendor, version and (when both buses exist) bus go into every name - see the placeholder table.
- Keep the AX version folder as the version suffix (`v_X_Y_x` -> `_v_X_Y_x`).
- Keep enum member names and values exactly.

## Workflow

0. **Locate and read everything** of the component (all `.st` in `ctrl/src` and `ctrl/test`, the twin `.cs`, the
   blazor view, `docs/`, assets). Use an Explore agent for a large component. Produce an inventory:
   classes and members, every task's step ladder, the IO byte/bit map with offsets, every messenger code with text + help,
   tests, simulator knobs, hardware template (module/slot shape).
1. **Grill** (tco-vendor-component Step 0, plus): bus (`Pn`/`Ecat`); library/FB names; which AX features to drop
   (hardware-ID bootstrap, numeric codes, TaskTimeout are dropped by default - see mapping); whether AX steps are ported 1:1
   (default yes, comment each block `// AX step N`). The interface map is the AX encode/decode code - no manual needed,
   but read the AX docs for unverified bits and keep those notes.
2. **Check the AX code before copying it.** AX components have had real bugs (e.g. a task that never reaches Done
   because of a wrong step number, a parameter stored but never used). Port the intent, tell the user about each
   deviation, record it in the README.
3. **Build** with tco-vendor-component: scaffold, register, IO, DUTs, functions, task base, tasks, component, twin, view.
   Translate with the mapping reference.
4. **Tests**: port the AX `FakeTemplate` harness into `TcoTemplateSimulator` (same knobs: fail/silent/stuck masks,
   timing counts, fault flags) and port each AxUnit test into an NUnit PLC-executed test. Tests of the AX hardware
   bootstrap (`S7Harness`, HW-ID codes) are dropped together with that feature. Codec tests become mapping tests
   (write raw bytes into the simulated input image, assert the decoded `_status`; set `_control`, assert output bytes).
5. **Localization**: neutral texts come from the PLC `<#...#>` literals. AX `PlcStringResources.<de|es|sk>.resx` and blazor
   `Strings.*.resx` are a translation source when the English text is the same - reuse their wording, then fill the gaps
   (cs-CZ never exists in AX). AX help texts (C# messenger list) are merged into the TcOpen message text where useful.
6. **README**: wiring, hardware template/slot table, device settings, byte maps, troubleshooting - from AX `docs/`.
   Copy GSDML/ESI from `ctrl/assets` to `assets/` and `TcoTemplateConnector/ddf/`.
7. **Report** to the user: what was dropped, AX bugs fixed, tests that could not be run.

## Verification without TwinCAT XAE

- **Library twin**: write a temporary filter in the repo root (`TcoTemplate.tmp.slnf`, `"path": "TcOpen.sln"`) with the
  library Connector, the Xae tsproj and the TestsConnector, then
  `.\_Vortex\builder\vortex.compiler.console.exe -s .\TcoTemplate.tmp.slnf -n false`. It parses the PLC declarations and
  regenerates `_generated/`; `WRN ... unknown type` on library types means a misspelt name. Delete the filter afterwards.
- **Test PLC twin**: the compiler resolves types of *referenced PLC libraries* from the TwinCAT managed library repository
  (`C:\TwinCAT\3.1\Components\Plc\Managed Libraries\Vortex.Library\TcoTemplate`). A brand-new library is not there until
  it is built and installed from XAE, so the test-connector twin (and the NUnit project) only compile after that - say so.
- **.NET**: the connector packs `_meta\TcoTemplate.library`, which only an XAE build produces. To check that the C#
  compiles earlier, put an empty placeholder file there for the build and delete it afterwards (`_meta/` is gitignored).
- ST implementation bodies are not checked by the Vortex compiler - review them, and build in XAE before release.
- In this environment bash heredocs collapse `\\`; write JSON (`.slnf`) with the Write tool or a script file.

## Extra definition-of-done items

- [ ] Nothing written to the AxOpen repository (`git -C <axopen> status` clean).
- [ ] Every AX task, operation and public data member is either ported or listed as dropped in the README.
- [ ] No AX identifiers or syntax left in the TcOpen code: `Axo*` types, `ix-attr`, `ix-set`, `REF_TO`, `LTIME`, `THIS.` (case-sensitive search; doc comments naming the AX origin are fine, and words like `RealTime` / `MaxOutputs` are false hits of a case-insensitive search).
- [ ] Every AX test is ported or listed as not applicable.

## Examples (reference ports)

Read them when a detail is unclear. Do not copy vendor names, signal names or texts from them into a new component.

| AX source | TcOpen port | What it shows |
|---|---|---|
| `components.ateq.measurement`, `AxoG6` `v_3_5_x` | `src/TcoAteqMeasurement`, `TcoAteqG6Pn_v_3_5_x` | cyclic PN with 4+4 modules mapped to one image, command handshake in an abstract task base, AX data classes as STRUCT + FUNCTIONs, simulator ported from `FakeG6`, numeric codes replaced by texts |
| `components.kuka.robotics`, `AxoKss` `v_9_3_x` | `src/TcoKukaRobotics`, `TcoKukaKssPn_v_9_3_x` / `...Ecat...` | 1:1 step port with `// AX step N`, both buses, AX bug fixed (start task never reached Done) |
