using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class GameManager : MonoBehaviour
{
    public GameObject tubePrefab;
    public GameObject ballPrefab;
    public Material[] ballMaterials;
    public GameObject winPopup;
    public TMP_Text moveCounterText;
    public TMP_Text levelText;

    public int tubeCapacity = 4;
    public int tubesPerRow = 4;
    public float tubeSpacingX = 1.2f;
    public float rowSpacingY = 3.5f;
    public float boardBaseHeight = 4f;

    public float tubeLiftHeight = 0.3f;
    public float ballClearanceAboveRim = 0.4f;
    public float liftMoveDuration = 0.15f;
    public float horizontalMoveDuration = 0.2f;
    public float dropDuration = 0.15f;

    GameObject[] tubes;
    GameObject selectedTube = null;
    GameObject liftedBall = null;
    Vector3 liftedBallRestingPosition;
    bool gameWon = false;
    bool isAnimating = false;
    int moveCount = 0;
    int currentLevel = 1;

    struct MoveRecord
    {
        public GameObject fromTube;
        public GameObject toTube;
        public GameObject ball;
    }

    Stack<MoveRecord> moveHistory = new Stack<MoveRecord>();

    void Start()
    {
        StartNewBoard();
    }

    int GetColorsForLevel(int level)
    {
        if (level <= 1) return 4;
        if (level == 2) return 5;
        return 6;
    }

    public void NextLevel()
    {
        currentLevel++;
        StartNewBoard();
    }

    public void RestartGame()
    {
        StartNewBoard();
    }

    void StartNewBoard()
    {
        if (tubes != null)
        {
            foreach (GameObject tubeObj in tubes)
            {
                if (tubeObj == null) continue;
                TubeController tc = tubeObj.GetComponent<TubeController>();
                foreach (GameObject ball in tc.ballsInTube)
                {
                    Destroy(ball);
                }
                Destroy(tubeObj);
            }
        }

        gameWon = false;
        isAnimating = false;
        selectedTube = null;
        liftedBall = null;
        moveCount = 0;
        moveHistory.Clear();
        winPopup.SetActive(false);
        UpdateMoveCounterDisplay();
        UpdateLevelDisplay();

        int colorsThisLevel = GetColorsForLevel(currentLevel);
        int totalTubes = colorsThisLevel + 2;

        GenerateTubes(totalTubes);

        List<List<int>> board = GenerateSolvableBoard(colorsThisLevel, totalTubes);

        for (int t = 0; t < tubes.Length; t++)
        {
            List<int> stack = board[t];
            for (int slot = 0; slot < stack.Count; slot++)
            {
                SpawnOneBall(tubes[t], slot, stack[slot]);
            }
        }
    }

    void GenerateTubes(int totalTubes)
    {
        tubes = new GameObject[totalTubes];
        int tubeIndex = 0;
        int rowCount = Mathf.CeilToInt(totalTubes / (float)tubesPerRow);

        for (int row = 0; row < rowCount; row++)
        {
            int remaining = totalTubes - tubeIndex;
            int countInRow = Mathf.Min(tubesPerRow, remaining);

            float totalWidth = (countInRow - 1) * tubeSpacingX;
            float startX = -totalWidth / 2f;

            for (int col = 0; col < countInRow; col++)
            {
                float x = startX + (col * tubeSpacingX);
                float y = boardBaseHeight - (row * rowSpacingY);
                Vector3 pos = new Vector3(x, y, 0);

                GameObject newTube = Instantiate(tubePrefab, pos, Quaternion.identity);
                tubes[tubeIndex] = newTube;
                tubeIndex++;
            }
        }
    }

    List<List<int>> GenerateSolvableBoard(int colorsThisLevel, int totalTubes)
    {
        List<List<int>> stacks = new List<List<int>>();
        for (int i = 0; i < totalTubes; i++)
        {
            stacks.Add(new List<int>());
        }

        for (int colorIndex = 0; colorIndex < colorsThisLevel; colorIndex++)
        {
            for (int n = 0; n < tubeCapacity; n++)
            {
                stacks[colorIndex].Add(colorIndex);
            }
        }

        int totalBalls = colorsThisLevel * tubeCapacity;
        int scrambleMoves = (totalBalls * 4) + (currentLevel * 5);

        for (int m = 0; m < scrambleMoves; m++)
        {
            int attempts = 0;
            bool moved = false;

            while (attempts < 50 && !moved)
            {
                attempts++;
                int from = Random.Range(0, totalTubes);
                int to = Random.Range(0, totalTubes);

                if (from == to) continue;
                if (stacks[from].Count == 0) continue;
                if (stacks[to].Count >= tubeCapacity) continue;

                int ballColor = stacks[from][stacks[from].Count - 1];
                stacks[from].RemoveAt(stacks[from].Count - 1);
                stacks[to].Add(ballColor);
                moved = true;
            }
        }

        return stacks;
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

        TubeController tc = selectedTube.GetComponent<TubeController>();
        if (tc.ballsInTube.Count > 0)
        {
            liftedBall = tc.ballsInTube[tc.ballsInTube.Count - 1];
            liftedBallRestingPosition = liftedBall.transform.position;
        }

        MoveTubeAndBalls(selectedTube, new Vector3(0, tubeLiftHeight, 0));

        if (liftedBall != null)
        {
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
    float duration = horizontalMoveDuration + dropDuration;

    float heightDifference = Mathf.Abs(finalPosition.y - startPosition.y);
    float arcHeight = Mathf.Max(0.8f, heightDifference * 0.6f);

    float elapsedTime = 0f;
    while (elapsedTime < duration)
    {
        elapsedTime += Time.deltaTime;
        float progress = elapsedTime / duration;

        float x = Mathf.Lerp(startPosition.x, finalPosition.x, progress);
        float z = Mathf.Lerp(startPosition.z, finalPosition.z, progress);
        float baseY = Mathf.Lerp(startPosition.y, finalPosition.y, progress);
        float arcBoost = Mathf.Sin(progress * Mathf.PI) * arcHeight;

        ball.transform.position = new Vector3(x, baseY + arcBoost, z);
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

    void UpdateLevelDisplay()
    {
        levelText.text = "Level " + currentLevel;
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