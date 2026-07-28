using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GameObject[] tubes;
    public GameObject ballPrefab;
    public Material[] ballMaterials;

    public int tubeCapacity = 4;
    public int filledTubeCount = 4;

    GameObject selectedTube = null;

    void Start()
    {
        SpawnBalls();
    }

    void SpawnBalls()
    {
        List<int> colorPool = new List<int>();
        for (int colorIndex = 0; colorIndex < ballMaterials.Length; colorIndex++)
        {
            for (int count = 0; count < tubeCapacity; count++)
            {
                colorPool.Add(colorIndex);
            }
        }

        for (int i = 0; i < colorPool.Count; i++)
        {
            int randomIndex = Random.Range(0, colorPool.Count);
            int temp = colorPool[i];
            colorPool[i] = colorPool[randomIndex];
            colorPool[randomIndex] = temp;
        }

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

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                TubeController clickedTube = hit.collider.GetComponent<TubeController>();
                if (clickedTube != null)
                {
                    HandleTubeClick(hit.collider.gameObject);
                }
            }
        }
    }

    void HandleTubeClick(GameObject clickedTube)
    {
        if (selectedTube == null)
        {
            // Nothing selected yet -> select this tube
            selectedTube = clickedTube;
            MoveTubeAndBalls(selectedTube, new Vector3(0, 0.3f, 0));
            Debug.Log("Selected: " + selectedTube.name);
        }
        else if (selectedTube == clickedTube)
        {
            // Clicked the same tube again -> just deselect
            MoveTubeAndBalls(selectedTube, new Vector3(0, -0.3f, 0));
            selectedTube = null;
            Debug.Log("Deselected");
        }
        else
        {
            // A different tube was clicked -> attempt to pour
            TryPour(selectedTube, clickedTube);

            // Always lower the source tube back down and clear selection
            MoveTubeAndBalls(selectedTube, new Vector3(0, -0.3f, 0));
            selectedTube = null;
        }
    }

    void TryPour(GameObject fromTubeObj, GameObject toTubeObj)
    {
        TubeController fromTube = fromTubeObj.GetComponent<TubeController>();
        TubeController toTube = toTubeObj.GetComponent<TubeController>();

        if (fromTube.ballsInTube.Count == 0)
        {
            Debug.Log("Source tube is empty, nothing to pour.");
            return;
        }

        if (toTube.ballsInTube.Count >= toTube.maxCapacity)
        {
            Debug.Log("Destination tube is full.");
            return;
        }

        GameObject topBall = fromTube.ballsInTube[fromTube.ballsInTube.Count - 1];
        BallController topBallData = topBall.GetComponent<BallController>();

        bool destinationIsEmpty = toTube.ballsInTube.Count == 0;
        bool colorsMatch = false;

        if (!destinationIsEmpty)
        {
            GameObject destTopBall = toTube.ballsInTube[toTube.ballsInTube.Count - 1];
            BallController destTopBallData = destTopBall.GetComponent<BallController>();
            colorsMatch = (destTopBallData.colorIndex == topBallData.colorIndex);
        }

        if (destinationIsEmpty || colorsMatch)
        {
            // Legal move - transfer the ball
            fromTube.ballsInTube.RemoveAt(fromTube.ballsInTube.Count - 1);

            int newStackPosition = toTube.ballsInTube.Count;
            Vector3 newPosition = GetBallPosition(toTubeObj, newStackPosition);
            topBall.transform.position = newPosition;

            toTube.ballsInTube.Add(topBall);

            Debug.Log("Poured a ball from " + fromTubeObj.name + " to " + toTubeObj.name);
        }
        else
        {
            Debug.Log("Illegal move: colors do not match.");
        }
    }

    Vector3 GetBallPosition(GameObject tube, int stackPosition)
    {
        float tubeBottomY = tube.transform.position.y - 1f;
        float ballRadius = 0.2f;
        float stackSpacing = 0.42f;

        return new Vector3(
            tube.transform.position.x,
            tubeBottomY + ballRadius + (stackPosition * stackSpacing),
            tube.transform.position.z
        );
    }

    void MoveTubeAndBalls(GameObject tube, Vector3 offset)
    {
        tube.transform.position += offset;

        TubeController tubeController = tube.GetComponent<TubeController>();
        foreach (GameObject ball in tubeController.ballsInTube)
        {
            ball.transform.position += offset;
        }
    }

    void SpawnOneBall(GameObject tube, int stackPosition, int colorIndex)
    {
        Vector3 spawnPosition = GetBallPosition(tube, stackPosition);
        GameObject newBall = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
        newBall.GetComponent<Renderer>().material = ballMaterials[colorIndex];

        BallController ballData = newBall.GetComponent<BallController>();
        ballData.colorIndex = colorIndex;

        TubeController tubeController = tube.GetComponent<TubeController>();
        tubeController.ballsInTube.Add(newBall);
    }
}