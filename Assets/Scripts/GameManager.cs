using UnityEngine;
using System.Collections;
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

    public float tubeLiftHeight = 0.3f;
    public float ballClearanceAboveRim = 0.4f;
    public float liftMoveDuration = 0.15f;
    public float horizontalMoveDuration = 0.2f;
    public float dropDuration = 0.15f;

    GameObject selectedTube = null;
    GameObject liftedBall = null;
    Vector3 liftedBallRestingPosition;
    bool gameWon = false;
    bool isAnimating = false;
    int moveCount = 0;

    struct MoveRecord
    {
        public GameObject fromTube;
        public GameObject toTube;
        public GameObject ball;
    }

    Stack<MoveRecord> moveHistory = new Stack<MoveRecord>();

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
        isAnimating = false;
        selectedTube = null;
        liftedBall = null;
        moveCount = 0;
        moveHistory.Clear();
        winPopup.SetActive(false);
        UpdateMoveCounterDisplay();

        SpawnBalls();
    }

    public void UndoMove()
    {
        if (gameWon) return;
        if (isAnimating) return;
        if (moveHistory.Count == 0) return;

        MoveRecord lastMove = moveHistory.Pop();

        TubeController toTube = lastMove.toTube.GetComponent<TubeController>();
        TubeController fromTube = lastMove.fromTube.GetComponent<TubeController>();

        toTube.ballsInTube.RemoveAt(toTube.ballsInTube.Count - 1);

        int newStackPosition = fromTube.ballsInTube.Count;
        Vector3 newPosition = GetBallPosition(lastMove.fromTube, newStackPosition);

        fromTube.ballsInTube.Add(lastMove.ball);

        StartCoroutine(SimpleMoveAnimation(lastMove.ball, newPosition, dropDuration));

        moveCount--;
        UpdateMoveCounterDisplay();
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
        if (isAnimating) return;

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
            SelectTube(clickedTube);
        }
        else if (selectedTube == clickedTube)
        {
            DeselectCurrentTube();
        }
        else
        {
            AttemptPour(selectedTube, clickedTube);
        }
    }

    void SelectTube(GameObject tube)
    {
        selectedTube = tube;
        MoveTubeAndBalls(selectedTube, new Vector3(0, tubeLiftHeight, 0));

        TubeController tc = selectedTube.GetComponent<TubeController>();
        if (tc.ballsInTube.Count > 0)
        {
            liftedBall = tc.ballsInTube[tc.ballsInTube.Count - 1];
            liftedBallRestingPosition = liftedBall.transform.position;

            float tubeTopY = tube.transform.position.y + 1f;
            float targetY = tubeTopY + ballClearanceAboveRim;
            Vector3 targetPos = new Vector3(liftedBall.transform.position.x, targetY, liftedBall.transform.position.z);

            StartCoroutine(SimpleMoveAnimation(liftedBall, targetPos, liftMoveDuration));
        }
    }

    void DeselectCurrentTube()
    {
        GameObject tubeToLower = selectedTube;
        GameObject ballToRestore = liftedBall;
        Vector3 restingPosition = liftedBallRestingPosition;

        selectedTube = null;
        liftedBall = null;

        if (ballToRestore != null)
        {
            StartCoroutine(SimpleMoveAnimation(ballToRestore, restingPosition, liftMoveDuration));
        }

        MoveTubeAndBallsExcept(tubeToLower, new Vector3(0, -tubeLiftHeight, 0), ballToRestore);
    }

    void AttemptPour(GameObject fromTubeObj, GameObject toTubeObj)
    {
        TubeController fromTube = fromTubeObj.GetComponent<TubeController>();
        TubeController toTube = toTubeObj.GetComponent<TubeController>();

        bool canPour = true;

        if (fromTube.ballsInTube.Count == 0)
        {
            canPour = false;
        }
        else if (toTube.ballsInTube.Count >= toTube.maxCapacity)
        {
            canPour = false;
        }
        else
        {
            BallController topBallData = liftedBall.GetComponent<BallController>();
            bool destinationIsEmpty = toTube.ballsInTube.Count == 0;

            if (!destinationIsEmpty)
            {
                GameObject destTopBall = toTube.ballsInTube[toTube.ballsInTube.Count - 1];
                BallController destTopBallData = destTopBall.GetComponent<BallController>();
                if (destTopBallData.colorIndex != topBallData.colorIndex)
                {
                    canPour = false;
                }
            }
        }

        if (!canPour)
        {
            DeselectCurrentTube();
            return;
        }

        GameObject ballToMove = liftedBall;

        fromTube.ballsInTube.RemoveAt(fromTube.ballsInTube.Count - 1);

        int newStackPosition = toTube.ballsInTube.Count;
        Vector3 finalPosition = GetBallPosition(toTubeObj, newStackPosition);

        toTube.ballsInTube.Add(ballToMove);

        MoveRecord record = new MoveRecord();
        record.fromTube = fromTubeObj;
        record.toTube = toTubeObj;
        record.ball = ballToMove;
        moveHistory.Push(record);

        moveCount++;
        UpdateMoveCounterDisplay();

        MoveTubeAndBallsExcept(fromTubeObj, new Vector3(0, -tubeLiftHeight, 0), ballToMove);

        selectedTube = null;
        liftedBall = null;

        StartCoroutine(PourBallAnimation(ballToMove, finalPosition));
    }

    IEnumerator PourBallAnimation(GameObject ball, Vector3 finalPosition)
    {
        isAnimating = true;

        Vector3 startPosition = ball.transform.position;
        Vector3 sidewaysTarget = new Vector3(finalPosition.x, startPosition.y, finalPosition.z);

        float elapsedTime = 0f;
        while (elapsedTime < horizontalMoveDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / horizontalMoveDuration;
            ball.transform.position = Vector3.Lerp(startPosition, sidewaysTarget, progress);
            yield return null;
        }
        ball.transform.position = sidewaysTarget;

        Vector3 dropStart = ball.transform.position;
        elapsedTime = 0f;
        while (elapsedTime < dropDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / dropDuration;
            ball.transform.position = Vector3.Lerp(dropStart, finalPosition, progress);
            yield return null;
        }
        ball.transform.position = finalPosition;

        isAnimating = false;

        CheckWinCondition();
    }

    IEnumerator SimpleMoveAnimation(GameObject ball, Vector3 targetPosition, float duration)
    {
        isAnimating = true;

        Vector3 startPosition = ball.transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            ball.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
            yield return null;
        }
        ball.transform.position = targetPosition;

        isAnimating = false;
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

    void MoveTubeAndBallsExcept(GameObject tube, Vector3 offset, GameObject excludedBall)
    {
        tube.transform.position += offset;

        TubeController tubeController = tube.GetComponent<TubeController>();
        foreach (GameObject ball in tubeController.ballsInTube)
        {
            if (ball == excludedBall) continue;
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