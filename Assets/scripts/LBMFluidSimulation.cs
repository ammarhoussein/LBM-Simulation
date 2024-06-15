using System.Runtime.InteropServices;
using UnityEngine;

public class LBMFluidSimulation : MonoBehaviour
{
    public int gridWidth = 50;
    public int gridHeight = 50;
    public int gridDepth = 50;
    public float relaxationTime = 1.0f;
    public Material velocityColorMaterial;
    public ComputeShader lbmComputeShader;
    public ComputeShader updateTextureComputeShader;

    private RenderTexture velocityFieldTexture;
    private ComputeBuffer fBuffer;
    private ComputeBuffer rhoBuffer;
    private ComputeBuffer uBuffer;
    private ComputeBuffer velocityFieldBuffer;

    private float[,,,] f; // Distribution functions
    private float[,,] rho; // Density
    private Vector3[,,] u; // Velocity

    private Vector3[] e = new Vector3[]
    {
        new Vector3(0, 0, 0),  // Rest

        new Vector3(1, 0, 0),  // Axis-aligned
        new Vector3(-1, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(0, -1, 0),
        new Vector3(0, 0, 1),
        new Vector3(0, 0, -1),

        new Vector3(1, 1, 0),  // Face-diagonal
        new Vector3(-1, -1, 0),
        new Vector3(1, -1, 0),
        new Vector3(-1, 1, 0),
        new Vector3(1, 0, 1),
        new Vector3(-1, 0, -1),
        new Vector3(1, 0, -1),
        new Vector3(-1, 0, 1),
        new Vector3(0, 1, 1),
        new Vector3(0, -1, -1),
        new Vector3(0, 1, -1),
        new Vector3(0, -1, 1)
    };

    private float[] w = new float[]
    {
        1.0f / 3.0f,     // Rest weight

        1.0f / 18.0f, 1.0f / 18.0f,  // Axis-aligned weights
        1.0f / 18.0f, 1.0f / 18.0f,
        1.0f / 18.0f, 1.0f / 18.0f,

        1.0f / 36.0f, 1.0f / 36.0f,  // Face-diagonal weights
        1.0f / 36.0f, 1.0f / 36.0f,
        1.0f / 36.0f, 1.0f / 36.0f,
        1.0f / 36.0f, 1.0f / 36.0f,
        1.0f / 36.0f, 1.0f / 36.0f,
        1.0f / 36.0f, 1.0f / 36.0f
    };

    void Start()
    {
        InitializeGrid();
        InitializeComputeBuffers();
        InitializeTexture3D();
    }

    void InitializeGrid()
    {
        f = new float[gridWidth, gridHeight, gridDepth, 19];
        rho = new float[gridWidth, gridHeight, gridDepth];
        u = new Vector3[gridWidth, gridHeight, gridDepth];
    
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    rho[x, y, z] = 1.0f;
                    u[x, y, z] = new Vector3(x % 2 == 0 ? 1.0f : 0.0f, y % 2 == 0 ? 1.0f : 0.0f, z % 2 == 0 ? 1.0f : 0.0f);
    
                    for (int k = 0; k < 19; k++)
                    {
                        float eu = Vector3.Dot(e[k], u[x, y, z]);
                        float u2 = Vector3.Dot(u[x, y, z], u[x, y, z]);
                        f[x, y, z, k] = w[k] * rho[x, y, z] * (1 + 3 * eu + 4.5f * eu * eu - 1.5f * u2);
                    }
                }
            }
        }
    }



    void InitializeComputeBuffers()
    {
        int numElements = gridWidth * gridHeight * gridDepth;
        int fSize = numElements * 19;
        int rhoSize = numElements;
        int uSize = numElements;
        int vector3Size = Marshal.SizeOf(typeof(Vector3));
    
        fBuffer = new ComputeBuffer(fSize, sizeof(float));
        rhoBuffer = new ComputeBuffer(rhoSize, sizeof(float));
        uBuffer = new ComputeBuffer(uSize, vector3Size);
        velocityFieldBuffer = new ComputeBuffer(uSize, vector3Size);
    
        // Initialize buffers with initial values
        fBuffer.SetData(f);
        rhoBuffer.SetData(rho);
        uBuffer.SetData(u);
    
        int kernelIndex = lbmComputeShader.FindKernel("CSMain");
        lbmComputeShader.SetInt("gridWidth", gridWidth);
        lbmComputeShader.SetInt("gridHeight", gridHeight);
        lbmComputeShader.SetInt("gridDepth", gridDepth);
        lbmComputeShader.SetFloat("relaxationTime", relaxationTime);
        lbmComputeShader.SetBuffer(kernelIndex, "f", fBuffer);
        lbmComputeShader.SetBuffer(kernelIndex, "rho", rhoBuffer);
        lbmComputeShader.SetBuffer(kernelIndex, "u", uBuffer);
    
        int updateTextureKernelIndex = updateTextureComputeShader.FindKernel("CSMain");
        updateTextureComputeShader.SetInt("gridWidth", gridWidth);
        updateTextureComputeShader.SetInt("gridHeight", gridHeight);
        updateTextureComputeShader.SetInt("gridDepth", gridDepth);
        updateTextureComputeShader.SetBuffer(updateTextureKernelIndex, "velocityFieldBuffer", velocityFieldBuffer);
    
        // Debug: Retrieve and print some data to ensure correct initialization
        Vector3[] debugData = new Vector3[10];
        uBuffer.GetData(debugData);
        for (int i = 0; i < debugData.Length; i++)
        {
            Debug.Log($"Velocity[{i}]: {debugData[i]}");
        }
    }
    

    void InitializeTexture3D()
    {
        velocityFieldTexture = new RenderTexture(gridWidth, gridHeight, 0, RenderTextureFormat.ARGBFloat);
        velocityFieldTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        velocityFieldTexture.volumeDepth = gridDepth;
        velocityFieldTexture.enableRandomWrite = true;
        velocityFieldTexture.Create();

        velocityColorMaterial.SetTexture("_VelocityField", velocityFieldTexture);

        int lbmKernelIndex = lbmComputeShader.FindKernel("CSMain");
        lbmComputeShader.SetTexture(lbmKernelIndex, "velocityField3D", velocityFieldTexture);

        int updateTextureKernelIndex = updateTextureComputeShader.FindKernel("CSMain");
        updateTextureComputeShader.SetTexture(updateTextureKernelIndex, "velocityFieldTexture", velocityFieldTexture);
        updateTextureComputeShader.SetBuffer(updateTextureKernelIndex, "velocityFieldBuffer", velocityFieldBuffer);
    }




    void Update()
    {
        LBMIteration();
        ApplyBoundaryConditions();
        UpdateVelocityFieldTexture();
    }

    void LBMIteration()
    {
        int threadGroupsX = Mathf.CeilToInt(gridWidth / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(gridHeight / 8.0f);
        int threadGroupsZ = Mathf.CeilToInt(gridDepth / 8.0f);

        int kernelIndex = lbmComputeShader.FindKernel("CSMain");
        lbmComputeShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);

        // Debug: Log after dispatching
        Debug.Log("LBMComputeShader dispatched");
    }

    void ApplyBoundaryConditions()
    {
        BounceBackOnGrid();
    }

    void BounceBackOnGrid()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    if (IsBoundaryNode(x, y, z))
                    {
                        for (int k = 0; k < 19; k++)
                        {
                            int x_new = x + (int)e[k].x;
                            int y_new = y + (int)e[k].y;
                            int z_new = z + (int)e[k].z;

                            if (x_new >= 0 && x_new < gridWidth && y_new >= 0 && y_new < gridHeight && z_new >= 0 && z_new < gridDepth)
                            {
                                f[x_new, y_new, z_new, k] = f[x, y, z, GetOppositeDirection(k)];
                            }
                        }
                    }
                }
            }
        }
    }
    int GetOppositeDirection(int k)
    {
        switch (k)
        {
            case 0: return 0;
            case 1: return 2;
            case 2: return 1;
            case 3: return 4;
            case 4: return 3;
            case 5: return 6;
            case 6: return 5;
            case 7: return 8;
            case 8: return 7;
            case 9: return 10;
            case 10: return 9;
            case 11: return 12;
            case 12: return 11;
            case 13: return 14;
            case 14: return 13;
            case 15: return 16;
            case 16: return 15;
            case 17: return 18;
            case 18: return 17;
            default: return -1;
        }
    }
    bool IsBoundaryNode(int x, int y, int z)
    {
        return x == 0 || x == gridWidth - 1 || y == 0 || y == gridHeight - 1 || z == 0 || z == gridDepth - 1;
    }

    void UpdateVelocityFieldTexture()
    {
        int numElements = gridWidth * gridHeight * gridDepth;
        Vector3[] velocityData = new Vector3[numElements];
        uBuffer.GetData(velocityData);
        velocityFieldBuffer.SetData(velocityData);
    
        int threadGroupsX = Mathf.CeilToInt(gridWidth / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(gridHeight / 8.0f);
        int threadGroupsZ = Mathf.CeilToInt(gridDepth / 8.0f);
    
        int updateTextureKernelIndex = updateTextureComputeShader.FindKernel("CSMain");
        updateTextureComputeShader.SetBuffer(updateTextureKernelIndex, "velocityFieldBuffer", velocityFieldBuffer);
        updateTextureComputeShader.SetTexture(updateTextureKernelIndex, "velocityFieldTexture", velocityFieldTexture);
        updateTextureComputeShader.Dispatch(updateTextureKernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
    
        // Debug: Log after dispatching
        Debug.Log("UpdateTextureComputeShader dispatched");
    }


    void OnDestroy()
    {
        fBuffer.Release();
        rhoBuffer.Release();
        uBuffer.Release();
        velocityFieldBuffer.Release();
    }
}
