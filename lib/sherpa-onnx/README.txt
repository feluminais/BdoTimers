sherpa-onnx.dll - the .NET wrapper of sherpa-onnx 1.13.8 (Apache 2.0, https://github.com/k2-fsa/sherpa-onnx), taken from
lib/net8.0 of the NuGet package org.k2fsa.sherpa.onnx 1.13.8.
SHA-256: 3761aa16b53e30026c46506e79c11ad2c2013aef6f922365ee93460db0b227e2

Kept here instead of referencing that package: it depends on the native runtime packages of all nine platforms, while
the app only needs org.k2fsa.sherpa.onnx.runtime.win-x64 (referenced normally, same version). Update both together.
