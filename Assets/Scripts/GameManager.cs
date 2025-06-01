using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using System.IO; // JSONファイルの入出力用
using Newtonsoft.Json; // JSONを扱うためのライブラリ



public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class CardData
    {
        public int unitId; // CSVのunit_id
        public string uniqueId;//CSVのunique_ID
        public Sprite sprite; // 対応するイラスト
    }

    public class UserData
    {
        public int highScore; // ハイスコア
        public List<string> correctCards; // 正解したカードのunique_idリスト
    }

     private const string FILE_PATH = "user_data.json"; // ユーザーデータファイルのパス
     private UserData userData; // ユーザーデータ


    public List<CardData> cardDatabase; // CSVデータから読み込むカード情報
    public Image[] handCards; // 手札のイラスト
    public Image problemCard; // 問題のイラスト
    public GameObject resultPanel; // 正解/不正解演出用パネル
    public Text judgeText; // 演出用テキスト
    public GameObject choicePanel; // 選択肢UI（続ける・終了ボタン）
    public Text timerText; // 制限時間表示用テキスト
    public Text currentScoreText; //現在のスコアを表示するテキスト
    public Text currentHighScore; //ハイスコアを表示するテキスト
    public Text currentScoreResult; //リザルト画面に現在のスコアを表示するテキスト
    public Text resultText; //リザルト演出で表示するテキスト
    public Button continueButton; // 続けるボタン
    public Button quitButton; // 終了ボタン
    public GameObject pausePanel; // ポーズ用パネル
    public Button pauseButton; // ポーズボタン
    public Button resumeButton; // 続行ボタン
    public Button quitToTitleButton; // タイトルに戻るボタン

    private int correctIndex; // 正解の手札のインデックス
    private float timeLimit = 30f; // 制限時間（秒）
    private float remainingTime; // 残り時間
    private bool isTimeRunning = false; // タイマーの状態を管理

    private List<CardData> handCardData; // 手札のカードデータ
    private int currentScore = 0; // 現在のスコア
    private int highScore = 0; // ハイスコア
    private int gameCount = 0; // 3回セットのカウント
    private int comboCount = 0; // 連続正解数


    void Start()
    {
        LoadUserData(); // ユーザーデータをロード
        LoadCSV(); // CSVのデータを読み込む
        StartGame();
        // ボタンのリスナーを設定
        continueButton.onClick.AddListener(ContinueGame);
        quitButton.onClick.AddListener(QuitGame);

        pauseButton.onClick.AddListener(PauseGame);
        resumeButton.onClick.AddListener(ResumeGame);
        quitToTitleButton.onClick.AddListener(ReturnToTitle);

        // 選択肢パネルを非表示にする
        choicePanel.SetActive(false);
        // ポーズパネルを非表示
        pausePanel.SetActive(false); 
    }

     // ユーザーデータをロード（なければ新規作成）
    void LoadUserData()
{
    string path = Path.Combine(Application.persistentDataPath, FILE_PATH);
    if (File.Exists(path))
    {
        string json = File.ReadAllText(path);
        userData = JsonConvert.DeserializeObject<UserData>(json); // ✅ 直接 userData に代入！

        if (userData == null)
        {
            Debug.LogError("userData の JSON 読み込みに失敗しました。新規データを作成します。");
            userData = new UserData { highScore = 0, correctCards = new List<string>() };
            SaveUserData();
        }
        else if (userData.correctCards == null)
        {
            userData.correctCards = new List<string>(); // ✅ correctCards が null なら初期化
        }
    }
    else
    {
        // 初期データ作成
        userData = new UserData
        {
            highScore = 0,
            correctCards = new List<string>()
        };
        SaveUserData();
    }
}

    // ユーザーデータを保存
    void SaveUserData()
    {
        string path = Path.Combine(Application.persistentDataPath, FILE_PATH);
        string json = JsonConvert.SerializeObject(userData, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    
    

    // ゲーム開始処理
    public void StartGame()
    {
        resultPanel.SetActive(false);
        choicePanel.SetActive(false);
        timerText.gameObject.SetActive(true);
        SetHandCards();
        SetProblemCard();
        StartTimer();
    }

    // CSVデータを読み込む
    void LoadCSV()
{
    // Resourcesフォルダ内のCSVファイルを読み込む
    TextAsset csvFile = Resources.Load<TextAsset>("unit_data"); // ファイル名（拡張子は不要）

    if (csvFile == null)
    {
        Debug.LogError("CSVファイルが見つかりません。Resourcesフォルダ内に配置してください。");
        return;
    }

    // CSVの内容を1行ごとに分割
    string[] lines = csvFile.text.Split('\n');

    cardDatabase = new List<CardData>(); // cardDatabaseを初期化

    for (int i = 1; i < lines.Length; i++) // ヘッダー行をスキップするために1から開始
    {
        string line = lines[i].Trim();

        // 空行をスキップ
        if (string.IsNullOrEmpty(line)) continue;

        // 行をカンマで分割
        string[] values = line.Split(',');

        if (values.Length < 3)
        {
            Debug.LogWarning($"行{i}のデータが不正です: {line}");
            continue;
        }

        // unit_idを取得
        if (!int.TryParse(values[0], out int unitId))
        {
            Debug.LogWarning($"行{i}のunit_idが不正です: {values[0]}");
            continue;
        }

        string uniqueId = values[2]; // unique_IDを取得
        // スプライトを取得
        string spritePath = values[1];
        Sprite sprite = Resources.Load<Sprite>(spritePath);

        if (sprite == null)
        {
            Debug.LogWarning($"行{i}のスプライトが見つかりません: {spritePath}");
            continue;
        }

        if (values.Length < 7) // 列数が足りない場合はスキップ
        {
            Debug.LogWarning($"行{i}のデータが不完全です: {line}");
            continue;
        }

        
         // CardDataオブジェクトを作成し、リストに追加
        cardDatabase.Add(new CardData
        {
            unitId = unitId,
            uniqueId=uniqueId,
            sprite = sprite,
        });
    }

    Debug.Log($"CSVから{cardDatabase.Count}枚のカードデータを読み込みました。");

    foreach (var card in cardDatabase)
{
    Debug.Log($"unitId: {card.unitId}, uniqueId: {card.uniqueId}, sprite: {card.sprite.name}");
}
Debug.Log($"CSVから{cardDatabase.Count}枚のカードデータを読み込みました。");
}


    // 手札を設定
    void SetHandCards()
{
    handCardData = new List<CardData>();
    List<CardData> shuffledCards = cardDatabase.OrderBy(x => Random.value).ToList();
    if (shuffledCards.Count == 0)
    {
        Debug.LogError("shuffledCardsが空です。CSVデータが正しく読み込まれているか確認してください。");
        return;
    }

    // 正解カードを決定
    CardData correctCard = shuffledCards[0];
    handCardData.Add(correctCard);
    Debug.Log($"正解カード: unitId={correctCard.unitId}, uniqueId={correctCard.uniqueId}");


    // 異なる unitId を持つカードのみを手札に追加する
    foreach (var card in shuffledCards)
    {
        if (handCardData.Count >= handCards.Length) break;
        if (!handCardData.Any(c => c.unitId == card.unitId)) // unitId が重複しないかチェック
        {
            handCardData.Add(card);
        }
    }

     // 手札のデータが正しく設定されているか確認
    if (handCardData.Count < handCards.Length)
    {
        Debug.LogError($"手札が不足しています！ 必要: {handCards.Length}, 現在: {handCardData.Count}");
    }


    // 手札のイラストをUIに反映
    for (int i = 0; i < handCards.Length; i++)
    {
        if (i >= handCardData.Count) break; // 安全策
        if (handCardData[i] == null)
        {
            Debug.LogError($"handCardData[{i}] が null です！");
        }
        else
        {
        handCards[i].sprite = handCardData[i].sprite;
        int index = i; // ローカルコピーを作成
        handCards[i].GetComponent<Button>().onClick.RemoveAllListeners();
        handCards[i].GetComponent<Button>().onClick.AddListener(() => OnCardClicked(index));

        Debug.Log($"カードボタン設定: index={index}, unitId={handCardData[i].unitId}, uniqueId={handCardData[i].uniqueId}");
        }
        
    }
}

    // 問題カードを設定
    // 問題カードを設定
void SetProblemCard()
{
    // 1: リストのcardDatabaseから、correctIndexのカードと同じunit_idを持つカードを抽出
    correctIndex = Random.Range(0, handCardData.Count); // 正解の手札のインデックスをランダムに決定
    CardData correctCard = handCardData[correctIndex]; // 正解カードを取得

    // 同じunit_idを持つカードをフィルタリング
    List<CardData> sameUnitIdCards = cardDatabase
        .Where(card => card.unitId == correctCard.unitId)
        .ToList();

    if (sameUnitIdCards.Count == 0)
    {
        Debug.LogError($"同じunit_idを持つカードが存在しません (unit_id: {correctCard.unitId})");
        return;
    }

    // 2: 抽出したカードのunique_idを比較
    CardData selectedCard = null;
    foreach (var card in sameUnitIdCards)
    {
        if (card.uniqueId != correctCard.uniqueId)
        {
            selectedCard = card;
            break;
        }
    }

    // 3: 違うunique_idのカードを設定
    if (selectedCard != null)
{
    
}
else
{
   
}

   

}

    // タイマー開始
    void StartTimer()
    {
        remainingTime = timeLimit;
        isTimeRunning = true;
    }

    void Update()
    {
        if (isTimeRunning)
        {
            remainingTime -= Time.deltaTime; // 残り時間を減少
            timerText.text = Mathf.Ceil(remainingTime).ToString() + "秒";

            if (remainingTime <= 0)
            {
                isTimeRunning = false;
                OnTimeUp();
            }
        }
    }


    // 時間切れ処理
    void OnTimeUp()
    {
        isTimeRunning = false;
        timerText.gameObject.SetActive(false); // 制限時間の表示を消す
        Debug.Log("時間切れ！");
        ShowChoicePanel();
    }

    void UpdateScoreUI(){
        if(currentScoreText != null)
        {
            currentScoreText.text="スコア:" + currentScore.ToString();
        }
    }

    // カードがクリックされたときの処理
    void OnCardClicked(int index)
    {
        if (!isTimeRunning) return; // タイマーが動いていない場合は無効

         if (handCardData == null || handCardData.Count == 0)
    {
        Debug.LogError("handCardDataがnullまたは空です！");
        return;
    }

    if (index < 0 || index >= handCardData.Count)
    {
        Debug.LogError($"無効なインデックスが指定されました: {index}");
        return;
    }

        isTimeRunning = false; // タイマーを停止
        timerText.gameObject.SetActive(false); // タイマーUIを非表示

        if (index == correctIndex)
        {
            comboCount++;
            int bonus = (comboCount > 1) ? 500 : 0; // 2連続目からボーナス付与
            currentScore += 1000 + bonus;

            //  userData の null チェックを追加
        if (userData == null)
        {
            Debug.LogError("userData が null です！ LoadUserData() が正しく実行されたか確認してください。");
            return;
        }

            // **正解したカードの unique_id を保存**
            string uniqueId = handCardData[index].uniqueId;
            if (!userData.correctCards.Contains(uniqueId)) // 重複防止
            {
                userData.correctCards.Add(uniqueId);
            }

            SaveUserData();

           
            UpdateScoreUI();
            ShowResult(true);
        }
        
        else
        {
            comboCount = 0;
            ShowResult(false);
        }
    }

    // 結果の表示
    void ShowResult(bool isCorrect)
    {
        resultPanel.SetActive(true);
        judgeText.text = isCorrect ? "正解！" : "不正解！";
        Invoke(nameof(EndRound), 2.0f); // 2秒後に選択肢を表示
    }

    //ラウンド終了処理(スコア比較時にもUIを更新)
    // ✅ ゲーム終了時にJSONを更新

    void EndRound()
    {
        gameCount++;

        if (gameCount >= 3)
        {
            // 3回プレイ後にスコア比較
            if (currentScore > highScore)
            {
                userData.highScore = currentScore;
                SaveUserData();
            }
            gameCount = 0; // カウントをリセット
            ShowChoicePanel();
        }
        else
        {
            StartGame(); // 次のラウンドを開始
        }

        UpdateScoreUI(); // スコア表示を更新


    }


    // 選択肢を表示
    void ShowChoicePanel()
    {
        choicePanel.SetActive(true);
        //ここでハイスコアを表示する
        currentHighScore.text="これまでのハイスコア:" + highScore.ToString();
        currentScoreResult.text="今回のスコア:" + currentScore.ToString();
         if (currentScore > highScore)
            {
                resultText.text="ハイスコア更新！";
            }
             else
        {
            resultText.text="ハイスコア更新失敗・・・";
        }



    }



    // ゲームを続ける
    public void ContinueGame()
    {
        choicePanel.SetActive(false); // パネルを非表示
        StartGame();
    }

    // ゲームを終了する
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("ゲーム終了"); // エディタで実行中の際にはこのログが出力されま
    }

    // ゲームを一時停止
    public void PauseGame()
    {
        isTimeRunning = false; // タイマーを停止
        pausePanel.SetActive(true); // ポーズパネルを表示
    }

    // ゲームを再開
    public void ResumeGame()
    {
        isTimeRunning = true; // タイマーを再開
        pausePanel.SetActive(false); // ポーズパネルを閉じる
    }

    // タイトルに戻る
    public void ReturnToTitle()
    {
        SceneManager.LoadScene("OutGame"); // タイトルシーンへ遷移
    }


}
