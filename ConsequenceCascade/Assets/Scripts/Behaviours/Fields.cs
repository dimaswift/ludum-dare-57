using ConsequenceCascade.Graphics;
using UnityEngine;

namespace ConsequenceCascade.Behaviours
{
    public class Fields : MonoBehaviour
    {
        [System.Serializable]  
        public class VisualizationFilter  
        {  
            public string name;  
            public string description;  
            public bool unlocked = false;  
            public int visualizationMode; // 0=Nodes, 1=Connections, 2=Energy, 3=Waves, 4=Combined  
            public Color primaryColor = Color.white;  
            public Color energyColor = Color.red;  
        }  

        [Header("Field Configuration")]  
        public int width = 256;  
        public int height = 256;  
        public int fieldLayers = 3;

        [Header("Simulation Parameters")]
        [Range(0.01f, 5f)] public float springRestDistance = 1;

        public int iterations = 5;
        public float speed = 1;
        public float angle = 1;
        public float density = 1;
        public float recession = 1;
        [Range(0.1f, 1000f)] public float[] springConstants = new float[3] { 1f, 2f, 3f };  
        [Range(0.01f, 1f)] public float[] dampingValues = new float[3] { 0.1f, 0.2f, 0.3f };  
        [Range(0.1f, 10f)] public float[] thresholds = new float[3] { 1f, 2f, 3f };  
        [Range(0.1f, 1f)] public float[] transferRates = new float[3] { 0.3f, 0.3f, 0.3f };  
        public bool[] invertTransfers = new bool[3] { false, true, false };  
        
        [Header("Visualization")]  
        public ComputeShader computeShader;

        public Material particleMaterial;
        public Mesh particleMesh;

        
        [Header("Debug")]
        public bool pauseSimulation = false;  
        
        // Internal variables  
        private ComputeBuffer[] fieldBuffers;
        private ParticleDrawer[] drawers;  
        
        private int initializeKernel;  
        private int updateKernel;  
        private int transferKernel;  
        private int disturbKernel;
        private int threadGroupsX;  
        private int threadGroupsY;  
        private bool initialized = false;

        private Camera cam;
        
        // Match the structures in the compute shader  
        struct FieldCell  
        {  
            public Vector2 position;  
            public Vector2 velocity;  
            public float energy;  
            public Vector2 force;

            // Default constructor  
            public FieldCell(Vector2 pos, Vector2 vel, float en, Vector2 f)  
            {  
                position = pos;  
                velocity = vel;  
                energy = en;  
                force = f;

            }  
        }  
        
        void OnEnable()  
        {  
            if (!initialized)  
            {  
                InitializeSimulation();  
                PrepareVisualization();  
                initialized = true;  
            }  
        }  
        
        void Start()  
        {  
            cam = Camera.main;
            if (!initialized)  
            {  
                InitializeSimulation();  
                PrepareVisualization();  
                initialized = true;  
            }  
        }  
        
        void Update()  
        {  
            if (!initialized) return;  
            
            if (!pauseSimulation)  
            {  
                UpdateSimulation();  
            }  
            UpdateVisualization();
        }  
        
        void InitializeSimulation()  
        {  
            // Validate compute shader  
            if (computeShader == null)  
            {  
                Debug.LogError("Compute shader is not assigned!");  
                return;  
            }  
            
            // Ensure parameter arrays are initialized with correct size  
            if (springConstants == null || springConstants.Length < fieldLayers)  
                springConstants = new float[fieldLayers];  
            if (dampingValues == null || dampingValues.Length < fieldLayers)  
                dampingValues = new float[fieldLayers];  
            if (thresholds == null || thresholds.Length < fieldLayers)  
                thresholds = new float[fieldLayers];  
            if (transferRates == null || transferRates.Length < fieldLayers)  
                transferRates = new float[fieldLayers];  
            if (invertTransfers == null || invertTransfers.Length < fieldLayers)  
                invertTransfers = new bool[fieldLayers];  
            
            // Default values if arrays are empty  
            for (int i = 0; i < fieldLayers; i++)  
            {  
                if (i >= springConstants.Length) springConstants[i] = 1.0f + i * 0.5f;  
                if (i >= dampingValues.Length) dampingValues[i] = 0.1f + i * 0.05f;  
                if (i >= thresholds.Length) thresholds[i] = 1.0f + i * 0.5f;  
                if (i >= transferRates.Length) transferRates[i] = 0.3f;  
                if (i >= invertTransfers.Length) invertTransfers[i] = (i % 2 == 1); // Alternate  
            }  
            
            // Get kernel indices  
            initializeKernel = computeShader.FindKernel("InitializeField");  
            updateKernel = computeShader.FindKernel("UpdateField");  
            transferKernel = computeShader.FindKernel("TransferEnergy");  
            disturbKernel = computeShader.FindKernel("DisturbField");  
            
            // Calculate thread groups  
            threadGroupsX = Mathf.CeilToInt(width / 8f);  
            threadGroupsY = Mathf.CeilToInt(height / 8f);  
            
            // Create field buffers  
            fieldBuffers = new ComputeBuffer[fieldLayers];  
            
            for (int i = 0; i < fieldLayers; i++)  
            {  
                // Create field buffer  
                int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FieldCell));  
                fieldBuffers[i] = new ComputeBuffer(width * height, stride);  
                
                // Initialize with zeros  
                FieldCell[] initialData = new FieldCell[width * height];  
                for (int j = 0; j < width * height; j++)  
                {  
                    initialData[j] = new FieldCell(Vector2.zero, Vector2.zero, 0f, Vector2.zero);  
                }  
                fieldBuffers[i].SetData(initialData);  
                
                // Initialize the field using compute shader  
                computeShader.SetInt("width", width);  
                computeShader.SetInt("height", height);  
                computeShader.SetBuffer(initializeKernel, "currentField", fieldBuffers[i]);
             
                computeShader.Dispatch(initializeKernel, threadGroupsX, threadGroupsY, 1);  
            }  
        }  
        
        void PrepareVisualization()  
        {  
            
            // Create instance buffers, args buffers and materials for each field layer  
            drawers = new ParticleDrawer[fieldLayers];  
            
            for (int i = 0; i < fieldLayers; i++)  
            {  
                // Create instance buffer for rendering  
                drawers[i] = new ParticleDrawer(particleMaterial, particleMesh, fieldBuffers[i]);
            }  
        }  
        
        void UpdateSimulation()  
        {  
            // Update each field layer  
            for (int i = 0; i < fieldLayers; i++)  
            {  
                // Set parameters for this layer  
                SetShaderParameters(i);  
                
                // Update field physics  

                computeShader.SetBuffer(updateKernel, "currentField", fieldBuffers[i]);
                computeShader.Dispatch(updateKernel, threadGroupsX, threadGroupsY, 1);  
                
                // // Transfer energy to next layer if not the last  
                // if (i < fieldLayers - 1)  
                // {  
                //     computeShader.SetBuffer(transferKernel, "currentField", fieldBuffers[i]);  
                //     computeShader.SetBuffer(transferKernel, "nextField", fieldBuffers[i + 1]);  
                //     computeShader.Dispatch(transferKernel, threadGroupsX, threadGroupsY, 1);  
                // }  
            }

            if (Input.GetMouseButton(0))
            {
                DisturbFieldAtWorldPosition(cam.ScreenToWorldPoint(Input.mousePosition), 1, 1);
            }
        }  
        
        void UpdateVisualization()  
        {  
            // For each field layer, update instance data and render  
            for (int i = 0; i < fieldLayers; i++)  
            {  
               drawers[i].Draw();
            }  
        }  
        
        void SetShaderParameters(int layerIndex)  
        {  
            if (layerIndex >= springConstants.Length || layerIndex >= dampingValues.Length ||   
                layerIndex >= thresholds.Length || layerIndex >= transferRates.Length ||   
                layerIndex >= invertTransfers.Length)  
                return;  
                
            computeShader.SetFloat("springConstant", springConstants[layerIndex]);  
            computeShader.SetFloat("damping", dampingValues[layerIndex]);  
            computeShader.SetFloat("threshold", thresholds[layerIndex]);  
            computeShader.SetFloat("energyTransferRate", transferRates[layerIndex]);  
            computeShader.SetBool("invertOnTransfer", invertTransfers[layerIndex]);  
            computeShader.SetFloat("deltaTime", Time.deltaTime);  
            computeShader.SetInt("width", width);  
            computeShader.SetInt("height", height);  
            computeShader.SetFloat("springRestDistance", springRestDistance);
            computeShader.SetFloat("speed", speed);
            computeShader.SetFloat("angle", angle);
            computeShader.SetFloat("density", density);
            computeShader.SetFloat("recession", recession);
            computeShader.SetInt("iterations", iterations);  
        }  
        
        // Method to disturb the field - can be called from player controller  
        public void DisturbField(Vector2 position, float strength, float radius)  
        {  
            // Convert position to grid coordinates (0-1 range to grid coords)  
            // Set parameters  
            computeShader.SetVector("disturbPosition", position);  
            computeShader.SetFloat("disturbStrength", strength);  
            computeShader.SetFloat("disturbRadius", radius);  
            
            // Apply to first field layer  
            computeShader.SetBuffer(disturbKernel, "currentField", fieldBuffers[0]);  
            computeShader.Dispatch(disturbKernel, threadGroupsX, threadGroupsY, 1);  
        }  
        
        // Overload for world position  
        public void DisturbFieldAtWorldPosition(Vector3 worldPosition, float strength, float radius)  
        {
            DisturbField(worldPosition, strength, radius);  
        }  
       
        // Reset all fields to zero state  
        public void ResetAllFields()  
        {  
            for (int i = 0; i < fieldLayers; i++)  
            {  
                computeShader.SetBuffer(initializeKernel, "currentField", fieldBuffers[i]);  
                computeShader.Dispatch(initializeKernel, threadGroupsX, threadGroupsY, 1);  
            }  
        }  
        
        // Debug information display  
        void DisplayDebugInfo()  
        {  
            // Create a debug readback buffer to sample field data  
            if (fieldBuffers != null && fieldBuffers.Length > 0)  
            {  
                FieldCell[] debugData = new FieldCell[10]; // Just sample a few cells  
                fieldBuffers[0].GetData(debugData, 0, 0, 10);  
                
                string debugText = "Field Layer 0 Sample:\n";  
                for (int i = 0; i < 5; i++)  
                {  
                    debugText += $"Cell {i}: Energy={debugData[i].energy:F2}, Vel={debugData[i].velocity.magnitude:F2}\n";  
                }  
                
                // Display in scene view or in a debug UI  
                Debug.Log(debugText);  
            }  
        }  
        
        // Clean up resources  
        void OnDisable()  
        {  
            CleanupBuffers();  
            initialized = false;  
        }  
        
        void OnDestroy()  
        {  
            CleanupBuffers();  
        }  
        
        void CleanupBuffers()  
        {  
            // Clean up all buffers  
            if (fieldBuffers != null)  
            {  
                foreach (var buffer in fieldBuffers)  
                {  
                    if (buffer != null) buffer.Release();  
                }  
            }  
          
        }  
    }
}
