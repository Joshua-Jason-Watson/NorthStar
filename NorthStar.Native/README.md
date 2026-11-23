# NorthStar.Native

Initial Windows x64 native bridge for LiteRT 2.4.0.

## First milestone

- Exposes a C-compatible ABI for .NET P/Invoke.
- Owns a shared LiteRT `Environment`.
- Loads a `.tflite` model using the CPU backend.
- Pre-allocates reusable input and output `TensorBuffer` objects.
- Keeps LiteRT C++ types behind opaque native handles.
- Reports errors through a thread-local UTF-8 error string.

The smoke test initializes and destroys the LiteRT runtime. It does not load a model or execute inference yet.
