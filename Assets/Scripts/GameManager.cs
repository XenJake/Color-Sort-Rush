using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GameObject[] tubes;
    public GameObject ballPrefab;
    public Material[] ballMaterials;

    public int tubeCapacity = 4;
    public int filledTubeCount = 4;

    void Start()
    {
        SpawnBalls();
    }

    void SpawnBalls()
    {
        // Step 1: Build a list of all ball colors we need (4 of each color)
        List<int> colorPool = new List<int>();
        for (int colorIndex = 0; colorIndex < ballMaterials.Length; colorIndex++)
        {
            for (int count = 0; count < tubeCapacity; count++)
            {
                colorPool.Add(colorIndex);
            }
        }

        // Step 2: Shuffle that list randomly
        for (int i = 0; i < colorPool.Count; i++)
        {
            int randomIndex = Random.Range(0, colorPool.Count);
            int temp = colorPool[i];
            colorPool[i] = colorPool[randomIndex];
            colorPool[randomIndex] = temp;
        }

        // Step 3: Fill the first N tubes using the shuffled colors
        int colorPoolPosition = 0;
        for (int tubeIndex = 0; tubeIndex < filledTubeCount; tubeIndex++)
        {
            for (int slot = 0; slot < tubeCapacity; slot++)
            {
                int colorToUse = colorPool[colorPoolPosition];
                colorPoolPosition++;

                SpawnOneBall(tubes[tubeIndex], slot, colorToUse);
            }
        }
    }

    void SpawnOneBall(GameObject tube, int stackPosition, int colorIndex)
    {
float tubeBottomY = tube.transform.position.y - 1f;
float ballRadius = 0.2f;
float stackSpacing = 0.42f;

Vector3 spawnPosition = new Vector3(
    tube.transform.position.x,
    tubeBottomY + ballRadius + (stackPosition * stackSpacing),
    tube.transform.position.z
);
        GameObject newBall = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
        newBall.GetComponent<Renderer>().material = ballMaterials[colorIndex];
    }
}