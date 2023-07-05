// StableFluids - A GPU implementation of Jos Stam's Stable Fluids on Unity
// https://github.com/keijiro/StableFluids

using UnityEngine;

namespace StableFluids
{
    public class Fluid : MonoBehaviour
    {
        #region Editable attributes

        [SerializeField] int _resolution = 512;
        [SerializeField] float _viscosity = 1e-6f;
        [SerializeField] float _force = 300;
        [SerializeField] float _exponent = 200;
        [SerializeField] Texture2D _texture1;
        [SerializeField] Texture2D _texture2;
        [SerializeField] Texture2D _noise;

        #endregion

        #region Internal resources

        [SerializeField, HideInInspector] ComputeShader _compute;
        [SerializeField, HideInInspector] Shader _shader;

        #endregion

        #region Private members

        Material _shaderSheet;
        Vector2 _previousInput;

        
        #region Custom Code 

        [Header("Custom Code")]

        public float FlowSpeed = 1.0f;
        public float CycleLength = 1.0f;

        public bool animate = true;
        public bool resetAtCycleEnd = true;
        [Range(0f, 1f)]
        [SerializeField] float _phase1 = 1;
        [Range(0f, 1f)]
        [SerializeField] float _phase2 = 1;
        public bool calculatesLerp = true;
        [Range(0f, 1f)]
        [SerializeField] float _lerpTo2 = 0;

        [SerializeField] DrawType _drawType = DrawType.ColorBuffer1;

        public enum DrawType
        {
            ColorBuffer1,
            ColorBuffer2,
            Velocity1,
            Velocity2,
            Velocity3,
            Pressure1,
            Pressure2
        }

        [SerializeField] Texture2D velocityMap;
        [SerializeField] bool _overrideVelocityMap;
        [SerializeField] DrawType _setVelocityMapTo = DrawType.Velocity1;

        [Header("Debug")]
        public MeshRenderer debugRenderer1;
        public MeshRenderer debugRenderer2;


        #endregion

        static class Kernels
        {
            public const int Advect = 0;
            public const int Force = 1;
            public const int PSetup = 2;
            public const int PFinish = 3;
            public const int Jacobi1 = 4;
            public const int Jacobi2 = 5;
        }

        int ThreadCountX { get { return (_resolution                                + 7) / 8; } }
        int ThreadCountY { get { return (_resolution * Screen.height / Screen.width + 7) / 8; } }

        int ResolutionX { get { return ThreadCountX * 8; } }
        int ResolutionY { get { return ThreadCountY * 8; } }

        // Vector field buffers
        static class VFB
        {
            public static RenderTexture V1;
            public static RenderTexture V2;
            public static RenderTexture V3;
            public static RenderTexture P1;
            public static RenderTexture P2;
        }

        // Color buffers (for double buffering)
        RenderTexture _color1A;
        RenderTexture _color1B;
        RenderTexture _color2A;
        RenderTexture _color2B;

        RenderTexture AllocateBuffer(int componentCount, int width = 0, int height = 0, bool allowRandomRW = true)
        {
            var format = RenderTextureFormat.ARGBHalf;
            if (componentCount == 1) format = RenderTextureFormat.RHalf;
            if (componentCount == 2) format = RenderTextureFormat.RGHalf;

            if (width  == 0) width  = ResolutionX;
            if (height == 0) height = ResolutionY;

            var rt = new RenderTexture(width, height, 0, format);
            rt.enableRandomWrite = allowRandomRW;
            rt.Create();
            return rt;
        }

        #endregion

        #region MonoBehaviour implementation

        void OnValidate()
        {
            _resolution = Mathf.Max(_resolution, 8);
        }

        void Start()
        {
            _shaderSheet = new Material(_shader);

            VFB.V1 = AllocateBuffer(2);
            VFB.V2 = AllocateBuffer(2);
            VFB.V3 = AllocateBuffer(2);
            VFB.P1 = AllocateBuffer(1);
            VFB.P2 = AllocateBuffer(1);

            _color1A = AllocateBuffer(4, _resolution, _resolution, false);
            _color1B = AllocateBuffer(4, _resolution, _resolution, false);
            _color2A = AllocateBuffer(4, _resolution, _resolution, false);
            _color2B = AllocateBuffer(4, _resolution, _resolution, false);

			if (debugRenderer2)
            	debugRenderer1.material.mainTexture = _color1A;
            if (debugRenderer2)
				debugRenderer2.material.mainTexture = _color2A;

            Reset1();
            Reset2();

        #if UNITY_IOS
            Application.targetFrameRate = 60;
        #endif

            _phase1 = 0.0f;
            _phase2 = CycleLength * 0.5f;
        }

        void OnDestroy()
        {
            Destroy(_shaderSheet);

            Destroy(VFB.V1);
            Destroy(VFB.V2);
            Destroy(VFB.V3);
            Destroy(VFB.P1);
            Destroy(VFB.P2);

            Destroy(_color1A);
            Destroy(_color1B);
            Destroy(_color2A);
            Destroy(_color2B);
        }


        void Reset1()
        {
            //_color1A.DiscardContents();
            Graphics.Blit(_texture1, _color1A);
        }
        void Reset2()
        {
            //_color2A.DiscardContents();
            Graphics.Blit(_texture2, _color2A);
        }

        void Update()
        {
            float timeDelta = Time.deltaTime;
       
            // Update the flow map offsets for both layers
            if (animate) {
                _phase1 += FlowSpeed * timeDelta;
                _phase2 += FlowSpeed * timeDelta;
            }
            if ( resetAtCycleEnd && _phase1 >= CycleLength )
            {
                _phase1 = 0.0f;
                Reset1();
            }
            if ( resetAtCycleEnd && _phase2 >= CycleLength )
            {
                _phase2 = 0.0f;
                Reset2();
            }
            if (calculatesLerp)
            {
                float HalfCycle = CycleLength / 2f;
                _lerpTo2 = ( Mathf.Abs(HalfCycle - _phase1) / HalfCycle );
            }

            if (_overrideVelocityMap)
            {
                RenderTexture target = null;
                switch (_setVelocityMapTo)
                {
                    case DrawType.ColorBuffer1: target = _color1A; break;
                    case DrawType.ColorBuffer2: target = _color2A; break;
                    case DrawType.Velocity1: target = VFB.V1; break;
                    case DrawType.Velocity2: target = VFB.V2; break;
                    case DrawType.Velocity3: target = VFB.V3; break;
                    case DrawType.Pressure1: target = VFB.P1; break;
                    case DrawType.Pressure2: target = VFB.P2; break;
                    default: Debug.LogError("Invalid DrawType " + _setVelocityMapTo); break;
                }
                Graphics.Blit(velocityMap, target);
            }

            var dt = Time.deltaTime;
            var dx = 1.0f / ResolutionY;

            // Input point
            var input = new Vector2(
                (Input.mousePosition.x - Screen.width  * 0.5f) / Screen.height,
                (Input.mousePosition.y - Screen.height * 0.5f) / Screen.height
            );

            // Common variables
            _compute.SetFloat("Time", 0);  // Unused
            _compute.SetFloat("DeltaTime", dt);
            _compute.SetFloat("FlowSpeed", FlowSpeed);

            // Advection
            _compute.SetTexture(Kernels.Advect, "U_in", VFB.V1);
            _compute.SetTexture(Kernels.Advect, "W_out", VFB.V2);
            _compute.Dispatch(Kernels.Advect, ThreadCountX, ThreadCountY, 1);

            // Diffuse setup
            var dif_alpha = dx * dx / (_viscosity * dt);
            _compute.SetFloat("Alpha", dif_alpha);
            _compute.SetFloat("Beta", 4 + dif_alpha);
            Graphics.CopyTexture(VFB.V2, VFB.V1);
            _compute.SetTexture(Kernels.Jacobi2, "B2_in", VFB.V1);

            // Jacobi iteration
            for (var i = 0; i < 20; i++)
            {
                _compute.SetTexture(Kernels.Jacobi2, "X2_in", VFB.V2);
                _compute.SetTexture(Kernels.Jacobi2, "X2_out", VFB.V3);
                _compute.Dispatch(Kernels.Jacobi2, ThreadCountX, ThreadCountY, 1);

                _compute.SetTexture(Kernels.Jacobi2, "X2_in", VFB.V3);
                _compute.SetTexture(Kernels.Jacobi2, "X2_out", VFB.V2);
                _compute.Dispatch(Kernels.Jacobi2, ThreadCountX, ThreadCountY, 1);
            }

            // Add external force
            _compute.SetVector("ForceOrigin", input);
            _compute.SetFloat("ForceExponent", _exponent);
            _compute.SetTexture(Kernels.Force, "W_in", VFB.V2);
            _compute.SetTexture(Kernels.Force, "W_out", VFB.V3);

            if (Input.GetMouseButton(1))
                // Random push
                _compute.SetVector("ForceVector", Random.insideUnitCircle * _force * 0.025f);
            else if (Input.GetMouseButton(0))
                // Mouse drag
                _compute.SetVector("ForceVector", (input - _previousInput) * _force);
            else
                _compute.SetVector("ForceVector", Vector4.zero);

            _compute.Dispatch(Kernels.Force, ThreadCountX, ThreadCountY, 1);

            // Projection setup
            _compute.SetTexture(Kernels.PSetup, "W_in", VFB.V3);
            _compute.SetTexture(Kernels.PSetup, "DivW_out", VFB.V2);
            _compute.SetTexture(Kernels.PSetup, "P_out", VFB.P1);
            _compute.Dispatch(Kernels.PSetup, ThreadCountX, ThreadCountY, 1);

            // Jacobi iteration
            _compute.SetFloat("Alpha", -dx * dx);
            _compute.SetFloat("Beta", 4);
            _compute.SetTexture(Kernels.Jacobi1, "B1_in", VFB.V2);

            for (var i = 0; i < 20; i++)
            {
                _compute.SetTexture(Kernels.Jacobi1, "X1_in", VFB.P1);
                _compute.SetTexture(Kernels.Jacobi1, "X1_out", VFB.P2);
                _compute.Dispatch(Kernels.Jacobi1, ThreadCountX, ThreadCountY, 1);

                _compute.SetTexture(Kernels.Jacobi1, "X1_in", VFB.P2);
                _compute.SetTexture(Kernels.Jacobi1, "X1_out", VFB.P1);
                _compute.Dispatch(Kernels.Jacobi1, ThreadCountX, ThreadCountY, 1);
            }

            // Projection finish
            _compute.SetTexture(Kernels.PFinish, "W_in", VFB.V3);
            _compute.SetTexture(Kernels.PFinish, "P_in", VFB.P1);
            _compute.SetTexture(Kernels.PFinish, "U_out", VFB.V1);
            _compute.Dispatch(Kernels.PFinish, ThreadCountX, ThreadCountY, 1);

            // Apply the velocity field to the color buffer.
            var offs = Vector2.one * (Input.GetMouseButton(1) ? 0 : 1e+7f);
            _shaderSheet.SetVector("_ForceOrigin", input + offs);
            _shaderSheet.SetFloat("_ForceExponent", _exponent);
            _shaderSheet.SetTexture("_VelocityField", VFB.V1);
            _shaderSheet.SetFloat("_Phase1", _phase1);
            _shaderSheet.SetFloat("_Phase2", _phase2);
            _shaderSheet.SetFloat("_LerpTo2", _lerpTo2);
            _shaderSheet.SetTexture("_Tex1", _color1A);
            _shaderSheet.SetTexture("_Tex2", _color2A);
            _shaderSheet.SetTexture("_Noise", _noise);
            Graphics.Blit(_color1A, _color1B, _shaderSheet, 0);
            Graphics.Blit(_color2A, _color2B, _shaderSheet, 0);


			// REMOVED because it was causing horrific lag
			// In case of new lag, maybe hte double-buffering was actually load-beating?...
			// In any case, I somehow ruined htis merely by adding a new set of textures. They endlessly polluted eachother.
            // Swap the color buffers.
            // var temp = _color1A;
            // _color1A = _color1B;
            // _color1B = temp;
            // //
            // var tmp2 = _color2A;
            // _color2A = _color2B;
            // _color2B = tmp2;

            _previousInput = input;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            DrawType drawType = _drawType;

            RenderTexture drawRT = null;
            switch(drawType)
            {
                case DrawType.ColorBuffer1: drawRT = _color1A; break;
                case DrawType.ColorBuffer2: drawRT = _color2A; break;
                case DrawType.Velocity1: drawRT = VFB.V1; break;
                case DrawType.Velocity2: drawRT = VFB.V2; break;
                case DrawType.Velocity3: drawRT = VFB.V3; break;
                case DrawType.Pressure1: drawRT = VFB.P1; break;
                case DrawType.Pressure2: drawRT = VFB.P2; break;
                default: Debug.LogError("Invalid DrawType " + _drawType); break;
            }
            Graphics.Blit(drawRT, destination, _shaderSheet, 0);
        }

        #endregion
    }
}
