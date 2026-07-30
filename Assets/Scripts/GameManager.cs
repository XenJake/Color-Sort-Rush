using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class GameManager : MonoBehaviour
{
    public GameObject[] tubes;
    public GameObject ballPrefab;
    public Material[] ballMaterials;
    public GameObject winPopup;
    public TMP_Text moveCounterText;

    public int tubeCapacity = 4;
    public int filledTubeCount = 4;

    GameObject selectedTube = null;
    bool gameWon = false;
    int moveCount = 0;

    void Start()
    {
        SpawnBalls();
        UpdateMoveCounterDisplay();
    }

    public void RestartGame()
    {
        foreach (GameObject tubeObj in tubes)
        {
            TubeController tube = tubeObj.GetComponent<TubeController>();
            foreach (GameObject ball in tube.ballsInTube)
            {
                Destroy(ball);
            }
            tube.ballsInTube.Clear();
        }

        gameWon = false;
        selectedTube = null;
        moveCount = 0;
        winPopup.SetActive(false);
        UpdateMoveCounterDisplay();

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
        if (gameWon) return;

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
            selectedTube = clickedTube;
            MoveTubeAndBalls(selectedTube, new Vector3(0, 0.3f, 0));
        }
        else if (selectedTube == clickedTube)
        {
            MoveTubeAndBalls(selectedTube, new Vector3(0, -0.3f, 0));
            selectedTube = null;
        }
        else
        {
            TryPour(selectedTube, clickedTube);

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
            return;
        }

        if (toTube.ballsInTube.Count >= toTube.maxCapacity)
        {
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
            fromTube.ballsInTube.RemoveAt(fromTube.ballsInTube.Count - 1);

            int newStackPosition = toTube.ballsInTube.Count;
            Vector3 newPosition = GetBallPosition(toTubeObj, newStackPosition);
            topBall.transform.position = newPosition;

            toTube.ballsInTube.Add(topBall);

            moveCount++;
            UpdateMoveCounterDisplay();

            CheckWinCondition();
        }
    }

    void UpdateMoveCounterDisplay()
    {
        moveCounterText.text = "Moves: " + moveCount;
    }

    void CheckWinCondition()
    {
        foreach (GameObject tubeObj in tubes)
        {
            TubeController tube = tubeObj.GetComponent<TubeController>();

            if (tube.ballsInTube.Count == 0)
            {
                continue;
            }

            if (tube.ballsInTube.Count < tube.maxCapacity)
            {
                return;
            }

            int firstColor = tube.ballsInTube[0].GetComponent<BallController>().colorIndex;
            foreach (GameObject ball in tube.ballsInTube)
            {
                int thisColor = ball.GetComponent<BallController>().colorIndex;
                if (thisColor != firstColor)
                {
                    return;
                }
            }
        }

        gameWon = true;
        winPopup.SetActive(true);
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