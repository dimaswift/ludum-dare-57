namespace ConsequenceCascade.Behaviours
{
    using UnityEngine;

    public class Waves : MonoBehaviour
    {
        
        [Header("Compute Shader")] public ComputeShader cellularWaveShader;

        [Header("Simulation Parameters")] public Vector2Int resolution = new Vector2Int(512, 512);
        [Range(0.001f, 0.1f)] public float simulationSpeed = 0.01f;
        [Range(0f, 0.1f)] public float damping = 0.01f;
        [Range(0f, 0.01f)] public float keplerianFactor = 0.001f;
        [Range(0.1f, 5f)] public float propagationSpeed = 1f;
        [Range(0f, 1f)] public float quantumFactor = 0.2f;
        [Range(0f, 0.5f)] public float energyThreshold = 0.1f;

        [Header("Mode Selection")] [Tooltip("0=Standard Wave, 1=Quantum-Inspired, 2=Orbital")] [Range(0, 2)]
        public int updateMode = 0;

        [Header("Visualization")] public Material visualizationMaterial;
        public bool autoAddDisturbances = false;
        public float disturbanceRate = 1f;
        public Vector2 disturbancePosition;

        // Shader resources  
        private RenderTexture currentStateRT;
        private RenderTexture nextStateRT;
        private RenderTexture visualizationRT;

        // Kernel IDs  
        private int initializeKernelID;
        private int simulationKernelID;
        private int swapBuffersKernelID;

        // Simulation state  
        private float timer = 0f;
        private float disturbanceTimer = 0f;
        private int frameCount = 0;
        private bool initialized = false;

        // Track mouse interactions  
        private Vector2 mousePos;
        private bool mouseDown = false;

        private void Start()
        {
            InitializeResources();
        }

        private void InitializeResources()
        {
            // Create render textures  
            currentStateRT = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.ARGBFloat);
            nextStateRT = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.ARGBFloat);
            visualizationRT = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.ARGBFloat);

            // Get kernel IDs  
            initializeKernelID = cellularWaveShader.FindKernel("InitializeWave");
            simulationKernelID = cellularWaveShader.FindKernel("CellularWave");
            swapBuffersKernelID = cellularWaveShader.FindKernel("SwapBuffers");

            // Bind resources  
            BindResources();

            // Initialize the simulation  
            InitializeSimulation();

            // Assign visualization texture to material  
            if (visualizationMaterial != null)
            {
                visualizationMaterial.mainTexture = visualizationRT;
            }

            initialized = true;
        }

        private void InitializeSimulation()
        {
            // Initialize the cellular automata  
            int threadGroupsX = Mathf.CeilToInt(resolution.x / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(resolution.y / 8.0f);

            // Set initialization parameters  
            cellularWaveShader.SetVector("centerPoint", new Vector2(resolution.x / 2f, resolution.y / 2f));
            cellularWaveShader.SetInt("width", resolution.x);
            cellularWaveShader.SetInt("height", resolution.y);

            // Dispatch initialization kernel  
            cellularWaveShader.Dispatch(initializeKernelID, threadGroupsX, threadGroupsY, 1);
        }

        private void Update()
        {
            if (!initialized || cellularWaveShader == null) return;

            // Track mouse input for interactive disturbances  
            HandleMouseInput();

            // Update simulation timer  
            timer += Time.deltaTime;
            disturbanceTimer += Time.deltaTime;

            // Run multiple steps per frame for faster simulation  
            float timeStep = simulationSpeed;
            int stepsPerFrame = 1;

            // Auto-add disturbances  
            if (autoAddDisturbances && disturbanceTimer > 1f / disturbanceRate)
            {
                disturbanceTimer = 0f;
                AddRandomDisturbance();
            }

            for (int i = 0; i < stepsPerFrame; i++)
            {
                RunSimulationStep(timeStep);
                frameCount++;
            }
        }

        private void RunSimulationStep(float timeStep)
        {
            // Update parameters  
            cellularWaveShader.SetFloat("deltaTime", timeStep);
            cellularWaveShader.SetFloat("damping", damping);
            cellularWaveShader.SetFloat("keplerianFactor", keplerianFactor);
            cellularWaveShader.SetFloat("propagationSpeed", propagationSpeed);
            cellularWaveShader.SetFloat("time", timer);
            cellularWaveShader.SetFloat("quantumFactor", quantumFactor);
            cellularWaveShader.SetFloat("energyThreshold", energyThreshold);
            cellularWaveShader.SetInt("updateMode", updateMode);
            cellularWaveShader.SetVector("centerPoint", new Vector2(resolution.x / 2f, resolution.y / 2f));
            cellularWaveShader.SetInt("width", resolution.x);
            cellularWaveShader.SetInt("height", resolution.y);
            cellularWaveShader.SetInt("frameCount", frameCount);
            cellularWaveShader.SetFloat("randomSeed", Random.value);

            // Dispatch compute shader  
            int threadGroupsX = Mathf.CeilToInt(resolution.x / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(resolution.y / 8.0f);

            // Run simulation kernel  
            cellularWaveShader.Dispatch(simulationKernelID, threadGroupsX, threadGroupsY, 1);

            // Swap buffers  
            cellularWaveShader.Dispatch(swapBuffersKernelID, threadGroupsX, threadGroupsY, 1);
        }

        private void BindResources()
        {
            // Bind textures to all kernels  
            cellularWaveShader.SetTexture(initializeKernelID, "CurrentState", currentStateRT);
            cellularWaveShader.SetTexture(initializeKernelID, "NextState", nextStateRT);

            cellularWaveShader.SetTexture(simulationKernelID, "CurrentState", currentStateRT);
            cellularWaveShader.SetTexture(simulationKernelID, "NextState", nextStateRT);
            cellularWaveShader.SetTexture(simulationKernelID, "Visualization", visualizationRT);

            cellularWaveShader.SetTexture(swapBuffersKernelID, "CurrentState", currentStateRT);
            cellularWaveShader.SetTexture(swapBuffersKernelID, "NextState", nextStateRT);
        }

        private RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format)
        {
            var rt = new RenderTexture(width, height, 0, format)
            {
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        private void OnDestroy()
        {
            // Clean up resources  
            ReleaseTexture(currentStateRT);
            ReleaseTexture(nextStateRT);
            ReleaseTexture(visualizationRT);
        }

        private void ReleaseTexture(RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
        }

        private void AddDisturbance(Vector2 position, float amplitude = 1.0f, float radius = 5.0f)
        {
            // Create a simple add-disturbance compute shader  
            ComputeShader disturbanceShader = cellularWaveShader; // We'll use the same shader  
            int disturbKernel = cellularWaveShader.FindKernel("InitializeWave"); // Reuse init kernel  

            // Set parameters for disturbance  
            cellularWaveShader.SetVector("centerPoint", position);

            // Dispatch only around the disturbance area (optimization)  
            int threadGroupsX = Mathf.CeilToInt(radius * 2 / 8.0f) + 1;
            int threadGroupsY = Mathf.CeilToInt(radius * 2 / 8.0f) + 1;

            // Add the disturbance (just reinitialize a small part of the texture)  
            cellularWaveShader.Dispatch(disturbKernel, threadGroupsX, threadGroupsY, 1);

            // Restore center point  
            cellularWaveShader.SetVector("centerPoint", new Vector2(resolution.x / 2f, resolution.y / 2f));
        }

        private void AddRandomDisturbance()
        {
            // Add random disturbance within a radius from center  
            float radius = Random.Range(50f, 200f);
            float angle = Random.Range(0f, Mathf.PI * 2f);

            Vector2 center = new Vector2(resolution.x / 2f, resolution.y / 2f);
            Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            AddDisturbance(position, Random.Range(0.5f, 1.0f), Random.Range(3f, 10f));
        }

        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                mouseDown = true;
                mousePos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                mouseDown = false;
            }

            if (mouseDown)
            {
                // Convert mouse position to simulation coordinates  
                Vector2 simulationPos = new Vector2(
                    (mousePos.x / Screen.width) * resolution.x,
                    (mousePos.y / Screen.height) * resolution.y
                );

                // Add disturbance at mouse position  
                AddDisturbance(simulationPos, 1.0f, 5.0f);
            }
        }

        // Method to be called from external scripts or UI  
        public void ToggleMode()
        {
            updateMode = (updateMode + 1) % 3;
        }

        public void SetKeplerianFactor(float factor)
        {
            keplerianFactor = factor;
        }
    }
}