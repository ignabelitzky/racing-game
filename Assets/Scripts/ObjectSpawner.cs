using UnityEngine;
using System.Collections;

public class ObjectSpawner : MonoBehaviour
{
    public GameObject objectPrefab; // Arrastra aquí el prefab del objeto en el Inspector
    public int numberOfObjects = 300;
    public float spawnDelay = 0.5f;
    public float minX = -450f;
    public float maxX = 450f;
    public float minZ = -450f;
    public float maxZ = 450f;
    public float minY = 70f;
    public float maxY = 300f;

    void Start()
    {
        StartCoroutine(SpawnObjects());
    }

    IEnumerator SpawnObjects()
    {
        for (int i = 0; i < numberOfObjects; i++)
        {
            // Generar posición random en X, Y y Z dentro de los rangos especificados
            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);
            float z = Random.Range(minZ, maxZ);

            // Crear la posición
            Vector3 position = new Vector3(x, y, z);

            // Sin rotación específica, pero puedes añadir si es necesario
            Quaternion rotation = Quaternion.identity;

            // Instanciar el objeto
            Instantiate(objectPrefab, position, rotation);

            // Esperar 0.5 segundos antes de spawnear el próximo objeto
            yield return new WaitForSeconds(spawnDelay);
        }
    }
}
