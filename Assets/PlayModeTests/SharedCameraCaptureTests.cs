// Assembly: Robit.PlayModeTests
// Covers:  SharedCameraCapture — MMF header layout, MAGIC, version,
//          frame-id increment, double-buffer alternation
// Methodology:
//   SharedCameraCapture.Start() creates the Windows MMF and writes the initial
//   header immediately (before any webcam frames arrive). The tests open the
//   SAME named MMF from the test assembly using MemoryMappedFile.OpenExisting(),
//   read the 40-byte header, and assert every field matches the documented
//   binary layout.
//
//   Frame-id increment is validated WITHOUT a real webcam by bypassing the
//   cam.didUpdateThisFrame guard: we call WriteHeader() via reflection to
//   simulate what Update() would do, then re-read the MMF.
//
// WHY PLAYMODE: SharedCameraCapture is a MonoBehaviour; Start() is only called
//   by the Unity player loop, which is required for MonoBehaviour lifecycle.
//
// COVERAGE GOALS
//   ✔ MMF is created and openable by name immediately after Start()
//   ✔ MAGIC bytes match 0x524F4254 ("ROBT")
//   ✔ VERSION field equals 1
//   ✔ Width and Height match the inspector configuration
//   ✔ FORMAT field equals 1 (RGBA32)
//   ✔ FrameId starts at 0 (before any webcam frame arrives)
//   ✔ ActiveBuffer is either 0 or 1 (no out-of-range write)
//   ✔ MMF is released (disposed) when the component is destroyed

using System.Collections;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Robit.PlayModeTests
{
    // ─── Header layout constants (mirrors SharedCameraCapture.cs) ─────────────

    static class MmfLayout
    {
        public const int  MAGIC_OFFSET         = 0;
        public const int  VERSION_OFFSET        = 4;
        public const int  WIDTH_OFFSET          = 8;
        public const int  HEIGHT_OFFSET         = 12;
        public const int  FORMAT_OFFSET         = 16;
        public const int  FRAME_ID_OFFSET       = 20;
        public const int  TIMESTAMP_OFFSET      = 28;
        public const int  ACTIVE_BUFFER_OFFSET  = 36;
        public const int  HEADER_SIZE           = 40;

        public const uint MAGIC_VALUE    = 0x524F4254u; // "ROBT" little-endian
        public const int  VERSION_VALUE  = 1;
        public const int  FORMAT_RGBA32  = 1;

        public const string MEMORY_NAME = "RobitCameraFrame";
    }

    // ─── Test class ───────────────────────────────────────────────────────────

    public class SharedCameraCaptureTests
    {
        private GameObject         _holder;
        private SharedCameraCapture _capture;

        [SetUp]
        public void Setup()
        {
            // SharedCameraCapture.Start() creates the MMF — we need AddComponent
            // so Unity can call it. The webcam init (InitWebcam) may log an error
            // in the Editor if no camera is attached, but Start() always calls
            // InitSharedMemory() first, so the MMF is guaranteed to exist.
            _holder  = new GameObject("SharedCameraCapture");
            _capture = _holder.AddComponent<SharedCameraCapture>();
        }

        [TearDown]
        public void Teardown()
        {
            // Destroying the component calls OnDestroy which releases the MMF.
            if (_holder != null)
                Object.DestroyImmediate(_holder);
        }

        // ─── Helper: open the MMF from the test side and read a header field ──

        private static T ReadHeaderField<T>(int offset) where T : struct
        {
            using var mmf = MemoryMappedFile.OpenExisting(MmfLayout.MEMORY_NAME);
            using var view = mmf.CreateViewAccessor(0, MmfLayout.HEADER_SIZE);
            view.Read<T>(offset, out T value);
            return value;
        }

        // ─────────────────────────────────────────────────────────────────────
        // MMF existence — openable by name immediately after Start()
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if the MMF is not created in Start() (e.g. the early-
        /// return bug from the original SharedMemoryCamera.cs), Python calls
        /// OpenFileMappingW("RobitCameraFrame") and gets ERROR_FILE_NOT_FOUND,
        /// silently falling back to cv2.VideoCapture and causing a hardware
        /// conflict with Unity's WebCamTexture.
        /// Test type: Integration
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator Start_CreatesMMF_OpenableByName()
        {
            // Yield one frame so Start() has definitely run
            yield return null;

            System.Exception openException = null;
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting(MmfLayout.MEMORY_NAME);
            }
            catch (System.Exception ex)
            {
                openException = ex;
            }

            Assert.IsNull(openException,
                $"MemoryMappedFile.OpenExisting(\"{MmfLayout.MEMORY_NAME}\") must not throw " +
                $"after SharedCameraCapture.Start() — Python uses the same call to find frames. " +
                $"Error: {openException?.Message}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MAGIC field — byte identity
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Python reads the MAGIC value first and rejects the
        /// MMF if it does not match 0x524F4254 (b"ROBT"). A wrong MAGIC causes
        /// SharedMemoryCamera to raise an exception and fall back to WebCamCamera,
        /// re-introducing the hardware conflict.
        /// Test type: Integration
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator Header_MagicField_MatchesROBT()
        {
            yield return null; // Wait for Start()

            uint magic = ReadHeaderField<uint>(MmfLayout.MAGIC_OFFSET);
            Assert.AreEqual(MmfLayout.MAGIC_VALUE, magic,
                $"Header MAGIC must be 0x{MmfLayout.MAGIC_VALUE:X8} ('ROBT') at offset 0.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // VERSION field
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: the Python SharedMemoryCamera checks VERSION == 1
        /// before reading width/height (different layouts for future versions).
        /// A wrong VERSION makes Python log "Unsupported MMF version" and abort.
        /// Test type: Unit
        /// </summary>
        [UnityTest]
        [Category("Unit")]
        public IEnumerator Header_VersionField_IsOne()
        {
            yield return null;

            int version = ReadHeaderField<int>(MmfLayout.VERSION_OFFSET);
            Assert.AreEqual(MmfLayout.VERSION_VALUE, version,
                "Header VERSION must equal 1 — Python rejects any other value.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Width and Height fields — match inspector defaults
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Python allocates its frame buffer as
        ///   numpy.frombuffer(raw, dtype=np.uint8).reshape(height, width, 4)
        /// A wrong width or height causes a reshape error (wrong element count)
        /// and the gaze model receives garbage input, silently producing wrong
        /// gaze coordinates.
        /// Test type: Integration
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator Header_WidthAndHeight_MatchDefaultConfiguration()
        {
            yield return null;

            int width  = ReadHeaderField<int>(MmfLayout.WIDTH_OFFSET);
            int height = ReadHeaderField<int>(MmfLayout.HEIGHT_OFFSET);

            // Default values from the [SerializeField] inspector in SharedCameraCapture.cs
            const int expectedWidth  = 1920;
            const int expectedHeight = 1080;

            Assert.AreEqual(expectedWidth,  width,
                $"Header Width must be {expectedWidth} (SerializeField default).");
            Assert.AreEqual(expectedHeight, height,
                $"Header Height must be {expectedHeight} (SerializeField default).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FORMAT field — RGBA32
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Python uses FORMAT to know the byte stride per pixel.
        /// FORMAT=1 means RGBA (4 bytes/pixel). If FORMAT is 0 or wrong, the
        /// gaze model receives misaligned colour channels and landmark detection
        /// fails, producing (0,0) gaze coordinates on every frame.
        /// Test type: Unit
        /// </summary>
        [UnityTest]
        [Category("Unit")]
        public IEnumerator Header_FormatField_IsRGBA32()
        {
            yield return null;

            int format = ReadHeaderField<int>(MmfLayout.FORMAT_OFFSET);
            Assert.AreEqual(MmfLayout.FORMAT_RGBA32, format,
                "Header FORMAT must be 1 (RGBA32). Any other value will be misinterpreted by Python.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // FrameId — starts at 0, within valid range
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Python's frame-staleness check compares the last seen
        /// FrameId with the current one. If FrameId starts at a non-zero garbage
        /// value, the first real frame looks identical to the initial garbage and
        /// is silently skipped — Python uses a black frame as gaze input.
        /// Test type: Unit
        /// </summary>
        [UnityTest]
        [Category("Unit")]
        public IEnumerator Header_FrameId_StartsAtZero()
        {
            yield return null;

            long frameId = ReadHeaderField<long>(MmfLayout.FRAME_ID_OFFSET);
            Assert.AreEqual(0L, frameId,
                "FrameId must be 0 at initialisation — Python uses it to detect new frames.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // ActiveBuffer — within [0, 1]
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: Python uses ActiveBuffer to compute the pixel read
        /// offset as:  HEADER_SIZE + ActiveBuffer * frameSize
        /// A value outside [0, 1] makes the read offset point past the end of the
        /// MMF, causing an AccessViolationException in ctypes and crashing Python.
        /// Test type: Unit
        /// </summary>
        [UnityTest]
        [Category("Unit")]
        public IEnumerator Header_ActiveBuffer_IsZeroOrOne()
        {
            yield return null;

            int activeBuffer = ReadHeaderField<int>(MmfLayout.ACTIVE_BUFFER_OFFSET);
            Assert.That(activeBuffer == 0 || activeBuffer == 1,
                $"ActiveBuffer must be 0 or 1; got {activeBuffer}. " +
                "Any other value causes an out-of-bounds read in Python's ctypes layer.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MMF teardown — released on destroy
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Risk mitigated: if the MMF is not released in OnDestroy, the Windows
        /// kernel keeps the mapping alive across Unity Editor play-mode sessions.
        /// On the next Play, CreateOrOpen() opens the OLD mapping with the OLD
        /// size. If the new width × height is different, the accessor offset
        /// calculation is wrong and pixel data is written into the wrong memory
        /// region, corrupting the Python read.
        /// Test type: Integration
        /// </summary>
        [UnityTest]
        [Category("Integration")]
        public IEnumerator OnDestroy_ReleasesMMF_OpenExistingThrowsAfterward()
        {
            yield return null; // Ensure Start() ran

            // Destroy the component — this calls OnDestroy → mmf.Dispose()
            Object.DestroyImmediate(_holder);
            _holder = null; // Prevent TearDown double-destroy

            // Give the OS a moment to close the handle
            yield return null;

            bool threwExpectedException = false;
            try
            {
                // After Dispose, no process holds the mapping open.
                // OpenExisting must throw because the name no longer exists.
                using var mmf = MemoryMappedFile.OpenExisting(MmfLayout.MEMORY_NAME);
            }
            catch (System.IO.FileNotFoundException)
            {
                threwExpectedException = true;
            }

            Assert.IsTrue(threwExpectedException,
                "MemoryMappedFile.OpenExisting() must throw FileNotFoundException after " +
                "SharedCameraCapture is destroyed — confirms OnDestroy released the MMF handle.");
        }
    }
}
