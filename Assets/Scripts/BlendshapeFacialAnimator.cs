using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml.Serialization;

public class BlendshapeFacialAnimator : BaseBlendshapeManager
{
    [Header("基本設定")]
    [Tooltip("通常:N 睡眠:S 失神:U")]
    public bool debugMode = false;
    public EmotionTagDefinition emotionTagDefinition;
    public BlendshapeMapping mapping;
    //restPoseを作成する際、ブレンドシェイプで作成されたデフォルト表情プロクシを持っているモデルの場合は
    //そのプロクシをここで指定することにより、指定されたプロクシに含まれるブレンドシェイプ群はrestPoseプロクシから除外されます
    //restPoseは表情がリセットされる際のゴールとなる状態を定義するプロクシなため、デフォルト表情用プロクシの内容を除外することで
    //表情リセット時にもデフォルト表情が保たれます。
    [Tooltip("デフォルト表情が定義されたProxyがあればそれを指定(例：neutral等)")]
    public List<string> proxyNamesToIgnore = new List<string>();
    BlendshapeProxy restPose;
    [Header("表情関連設定")]
    public float emotionTransitionDuration = 0.3f;
    [Tooltip("目を閉じた際に通常瞬きと、笑い瞬きをブレンドする比率")]
    [Range(0f, 1f)]
    public float joy = 0f;
    private float lastJoy = 0f;
    public enum Consciousness
    {
        Normal,
        Unconscious,
        Sleeping,
    }
    [Header("意識レベル")]
    [Tooltip("consciousnessLevelを指定することで意識レベルを変更")]
    public Consciousness consciousnessLevel;
    [Tooltip("Sleepiness != 0 だと半目状態での瞬き")]
    [Range(0f, 1f)]
    public float sleepiness = 0f;
    private float lastSleepiness = 0f;
    [Range(0f, 1f)]
    public float maxSleepiness = 0.75f;
    private float lastMaxSleepiness = 0.75f;
    [Header("参照用(意識レベルの変更はconsciounessLevelの指定で行うこと)")]
    public bool isSleeping = false;
    public bool isUnconscious = false;

    // === Blink timing parameters ===========================================
    [Header("瞬き関連")]
    public AnimationCurve blinkAnimation;
    public BlendshapeProxy blink;
    public BlendshapeProxy blink_joy;
    [Min(0.5f)] public float minBlinkInterval = 3f;   // 次の瞬きまでの最短秒数
    [Min(0.5f)] public float maxBlinkInterval = 7f;   // 次の瞬きまでの最長秒数
    [Range(0f, 1f)] public float doubleBlinkChance = 0.25f; // ダブルブリンク確率
    [Tooltip("ダブルブリンク時の2回目までの間隔")]
    public float doubleBlinkGap = 0.15f;              // 2 回目までの待機時間
    [Header("Blink Boost")]
    [Tooltip("1.0 = 通常。大きいほど瞬き間隔が短くなり頻度が上がる")]
    [Min(0f)]
    public float blinkFrequencyMultiplier = 1.0f;                                                  //-----------------------------------------------------------------------
    float _nextBlinkTime;                             // 次に瞬きを起こす時刻
    [Tooltip("1回の瞬きにかかる秒数")]
    public float blinkDuration = 0.1f;
    [Header("参照用")]
    public bool blinking = false;


    SkinnedMeshRenderer skinnedMeshRenderer;
    Dictionary<int, float> restPoseMap;
    List<float> blendshapeCache = new List<float>();
    bool blendshapeUpdateNeeded = false;

    [System.Serializable]
    public class EmotionTag
    {
        public string emotion;
        public int magnitude;
    }

    private List<EmotionTag> currentEmotionTags = new List<EmotionTag>();
    private List<EmotionTag> lastEmotionTags = new List<EmotionTag>();
    private float savedCount = 0f;
    private Coroutine currentBlinkCoroutine;
    private float blinkPeakTime;
    void Start()
    {
        CreateRestPoseProxy();

        // restPose を辞書化
        // restPose を index → value の辞書にして高速検索
        restPoseMap = new Dictionary<int, float>();
        foreach (var v in restPose.blendshapes)
            restPoseMap[v.index] = v.value;

        blink = FindProxy("blink");
        blink_joy = FindProxy("blink_joy");
        if (debugMode)
        {
            if(blink == null)
            {
                Debug.Log("<color=orange>Cannot Find blink<color>");
            } else
            {
                Debug.Log("<color=cyan>Found blink<color>");

            }
            if(blink_joy == null)
            {
                Debug.Log("<color=orange>Cannot Find blink_joy<color>");
            } else
            {
                Debug.Log("<color=cyan>Found blink_joy<color>");

            }
        }

        skinnedMeshRenderer = mapping.skinnedMeshRenderer;
        InitializeBlendshapeCache();
        _nextBlinkTime = Time.time + Random.Range(minBlinkInterval, maxBlinkInterval);
        //瞬き用アニメーションカーブのピークをキャッシュ
        blinkPeakTime = FindClosingPeak(blinkAnimation, 200);
    }

    //private void Start()
    //{
    //    //必要な処理があれば実装    
    //}

    // Update is called once per frame
    void LateUpdate()
    {
        switch (consciousnessLevel)
        {
            case Consciousness.Normal:
                if (isUnconscious)
                {
                    ExitUnconscious();
                    break;
                }
                if (isSleeping)
                {
                    ExitSleeping();
                }
                //アニメーションの管理を実装
                RandomBlink();
                break;
            case Consciousness.Unconscious:
                if (!isUnconscious)
                    EnterUnconscious();
                break;
            case Consciousness.Sleeping:
                if ((!isSleeping))
                    EnterSleep();
                break;
        }

        //ApplyEmotionTagが呼ばれるとblendshapeUpdateNeededフラグを立て、以下をトリガーする
        if (blendshapeUpdateNeeded)
        {
            UpdateBlendshpes();
            blendshapeUpdateNeeded = false;
        }
        if ((sleepiness != lastSleepiness) || (joy != lastJoy) || (maxSleepiness != lastMaxSleepiness))
            OnSleepinessChanged();

        lastSleepiness = sleepiness;
        lastJoy = joy;
        lastMaxSleepiness = maxSleepiness;

        if (debugMode)
            EditorDebugMode();
    }

    public void CreateRestPoseProxy()
    {
        List<BlendshapeValue> blendshapeValues = new List<BlendshapeValue>();

        foreach (BlendshapeProxy proxy in mapping.proxies)
        {
            foreach (BlendshapeValue blendshapeValue in proxy.blendshapes)
            {
                var newValue = new BlendshapeValue(
                    blendshapeValue.index,
                    blendshapeValue.blendshapeName,
                    blendshapeValue.value);
                if (!proxyNamesToIgnore.Contains(proxy.name))
                    newValue.value = 0f;
                blendshapeValues.Add(newValue);
            }
        }

        restPose = new BlendshapeProxy
        {
            name = "neutralBase",
            blendshapes = blendshapeValues
        };
    }
    
    BlendshapeProxy FindProxy(string name)
    {
        foreach(BlendshapeProxy proxy in mapping.proxies) { if (proxy.name == name) return proxy; }
        return null;
    }

    void ApplyProxyBlendshapes(BlendshapeProxy proxy, float rate)
    {
        foreach (BlendshapeValue v in proxy.blendshapes)
        {
            // restPose に同じ index があるか調べる
            if (restPoseMap.TryGetValue(v.index, out float restValue))
            {
                // restValue ～ v.value を 0～1 とみなして補間
                float weight = Mathf.Lerp(restValue, v.value, Mathf.Clamp01(rate));
                if(!Mathf.Approximately(blendshapeCache[v.index], weight))
                {
                    skinnedMeshRenderer.SetBlendShapeWeight(v.index, weight);
                    blendshapeCache[v.index] = weight;
                }
            }
            else
            {
                skinnedMeshRenderer.SetBlendShapeWeight(v.index, v.value * rate);
                blendshapeCache[v.index] = v.value;
            }
        }
    }

    void InitializeBlendshapeCache()
    {
        for(int i = 0;i < skinnedMeshRenderer.sharedMesh.blendShapeCount;i++)
        {
            blendshapeCache.Add(skinnedMeshRenderer.GetBlendShapeWeight(i));
        }
    }
    void OnSleepinessChanged()
    {
        if (blinking)
            return;

        ApplyProxyBlendshapes(blink, sleepiness * maxSleepiness * (1f - joy));
        ApplyProxyBlendshapes(blink_joy, sleepiness * maxSleepiness * joy);
    }
    void EnterUnconscious()
    {
        float s = 1.0f / blinkDuration;
        if(currentBlinkCoroutine != null)
            StopCoroutine(currentBlinkCoroutine);
        isUnconscious = true;
        blinking = false;
    }
    void ExitUnconscious()
    {
        float s = 1.0f / blinkDuration;
        StartCoroutine(ExitUnconsciousCoroutine(s));
    }
    IEnumerator ExitUnconsciousCoroutine(float speed)
    {
        isUnconscious = false;
        float count = savedCount;
        blinking = true;
        while (count <= 1.0f)
        {
            float v = blinkAnimation.Evaluate(count);
            ApplyProxyBlendshapes(blink, v * (1.0f - joy));
            ApplyProxyBlendshapes(blink_joy, v * joy);
            count += Time.deltaTime * speed;
            savedCount = count;
            yield return null;
        }
        ApplyProxyBlendshapes(blink, sleepiness * maxSleepiness * (1f - joy));
        ApplyProxyBlendshapes(blink_joy, sleepiness * maxSleepiness * joy);
        blinking = false;
    }
    void EnterSleep()
    {
        float s = 1.0f / blinkDuration;
        if(currentBlinkCoroutine != null)
            StopCoroutine(currentBlinkCoroutine);
        StartCoroutine(EnterSleepCoroutine(s));
    }

    IEnumerator EnterSleepCoroutine(float speed)
    {
        isSleeping = true;
        // 1. 今の開き具合 v を取得
        float v = blinkAnimation.Evaluate(savedCount);        // 0〜1

        // 2. 直線 y = (1/peak) x で t を近似
        float t = v * blinkPeakTime;                          // 0〜peak

        // 3. すでに t を追い越していたら savedCount を優先
        if (savedCount > t && savedCount < blinkPeakTime)
            t = savedCount;

        float count = t;
        blinking = true;

        while (count < blinkPeakTime)
        {
            float val = blinkAnimation.Evaluate(count);
            ApplyProxyBlendshapes(blink, val * (1f - joy));
            ApplyProxyBlendshapes(blink_joy, val * joy);

            count += Time.deltaTime * speed;
            savedCount = count;
            yield return null;
        }

        // 5. 完全に閉じた状態で眠りへ
        ApplyProxyBlendshapes(blink, 1f * (1f - joy));
        ApplyProxyBlendshapes(blink_joy, 1f * joy);

        savedCount = blinkPeakTime;
        blinking = true;
    }


    void ExitSleeping()
    {
        float s = 1.0f / blinkDuration;
        StartCoroutine(ExitSleepingCoroutine(s));
    }
    IEnumerator ExitSleepingCoroutine(float speed)
    {
        isSleeping = false;
        float count = savedCount;
        blinking = true;
        while (count <= 1.0f)
        {
            float v = blinkAnimation.Evaluate(count);
            ApplyProxyBlendshapes(blink, v * (1.0f - joy));
            ApplyProxyBlendshapes(blink_joy, v * joy);
            count += Time.deltaTime * speed;
            savedCount = count;
            yield return null;
        }
        //ApplyProxyBlendshapes(blink, 0 * (1.0f - joy));
        //ApplyProxyBlendshapes(blink_joy, 0 * joy);
        ApplyProxyBlendshapes(blink, sleepiness * maxSleepiness * (1f - joy));
        ApplyProxyBlendshapes(blink_joy, sleepiness * maxSleepiness * joy);

        blinking = false;
    }

    void Blink()
    {
        if (blinking) 
            return;

        float s = 1.0f / blinkDuration;
        currentBlinkCoroutine = StartCoroutine(BlinkCoroutine(s));
    }

    IEnumerator BlinkCoroutine(float speed)
    {
        float count = 0f;
        blinking = true;

        while (count <= 1f)
        {
            // 0→1→0
            float blinkFactor = blinkAnimation.Evaluate(count);

            // sleepiness を基準に閉じる
            float v = Mathf.Lerp(sleepiness * maxSleepiness, 1f, blinkFactor);

            ApplyProxyBlendshapes(blink, v * (1f - joy));
            ApplyProxyBlendshapes(blink_joy, v * joy);

            count += Time.deltaTime * speed;
            yield return null;
        }

        // まばたき終了 → 基準値に戻す
        ApplyProxyBlendshapes(blink, sleepiness * maxSleepiness * (1f - joy));
        ApplyProxyBlendshapes(blink_joy, sleepiness * maxSleepiness * joy);

        blinking = false;
    }

    void RandomBlink()
    {
        // すでに瞬き中なら何もしない
        if (blinking) return;

        // 予定時刻を過ぎたら瞬き開始
        if (Time.time >= _nextBlinkTime)
        {
            StartCoroutine(BlinkSequence());

            // 次回の予定を決め直す
            float interval = Random.Range(minBlinkInterval, maxBlinkInterval);
            interval /= Mathf.Max(0.0001f, blinkFrequencyMultiplier);   // ゼロ割り防止
            _nextBlinkTime = Time.time + interval;
        }
    }

    IEnumerator BlinkSequence()
    {
        // 1 回目
        Blink();
        // Blink() 内で blinking=true になるので終わるまで待機
        yield return new WaitWhile(() => blinking);

        // ---- ダブルブリンク判定 --------------------------------------
        if (Random.value < doubleBlinkChance)
        {
            yield return new WaitForSeconds(doubleBlinkGap);
            Blink();                                 // 2 回目
            yield return new WaitWhile(() => blinking);
        }
    }

    //LLM API Wrapperがテキスト取得時に発行するイベントで発火
    //テキストから感情タグを抽出し、currentEmotionTagsを更新した状態で、SBV2Managerの音声再生開始イベントを待つ
    public void EmotionTagDecorder(string assistantReply)
    {

        //Debug.Log($"<color=yellow>Tags to Process;</color><color=cyan>{assistantReply}</color>");

        currentEmotionTags.Clear();

        if (string.IsNullOrEmpty(assistantReply) || emotionTagDefinition == null)
            return;

        // 例: [Joy:3]     [Anger:-1.2]   [Surprise]
        // ① タグ名 ([A-Za-z]+)       ② 数値部分 (任意) -> [0-9.+-]+
        var pattern = @"\[\s*([A-Za-z]+)\s*(?::\s*([0-9.+-]+)\s*)?\]";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        // 後勝ち用の辞書 (キー比較は大小文字無視)
        var latest = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        foreach (Match m in regex.Matches(assistantReply))
        {
            if (!m.Success) continue;

            string tagName = m.Groups[1].Value.ToLower();   // 全て lower-case に統一

            // 定義済みタグのみ対象
            bool isDefined = emotionTagDefinition.emotionTags
                .Any(t => t.Equals(tagName, System.StringComparison.OrdinalIgnoreCase));

            if (!isDefined) continue;

            // 既定値 3
            int magnitude = 3;

            if (m.Groups[2].Success)
            {
                string numStr = m.Groups[2].Value;

                if (!int.TryParse(numStr, out magnitude))      // 整数として失敗したら
                {
                    // 小数など → double にして四捨五入
                    if (double.TryParse(numStr, out double dbl))
                        magnitude = (int)System.Math.Round(dbl);
                }
            }

            // 後から来た値で上書き
            latest[tagName] = magnitude;
        }

        // 辞書 → currentEmotionTags へ反映
        foreach (var kv in latest)
        {
            currentEmotionTags.Add(new EmotionTag
            {
                emotion = kv.Key,
                magnitude = kv.Value
            });
        }
    }

    private int GetProxyIndex(string tag)
    {
        // ── 安全確認 ─────────────────────────────────────────────
        if (string.IsNullOrEmpty(tag) || mapping == null || mapping.proxies == null)
            return -1;

        // ── 線形検索。見付かればその時点でインデックスを返す ──
        for (int i = 0; i < mapping.proxies.Count; i++)
        {
            var proxy = mapping.proxies[i];
            if (proxy != null && proxy.name == tag)
                return i;
        }

        // ── 該当なし ─────────────────────────────────────────────
        return -1;
    }

    //SBV2ManagerのVoiceStartイベントで発火
    //作成済みのcurrentEmotionTagsをブレンドシェイプに適応
    public void ApplyEmotionTags()
    {
        blendshapeUpdateNeeded = true;
    }

    float FindClosingPeak(AnimationCurve curve, int samples = 100)
    {
        float prev = curve.Evaluate(0f);

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;          // 0.0 → 1.0
            float val = curve.Evaluate(t);

            // 上昇 → 下降に切り替わった瞬間＝ピークを過ぎた
            if (val < prev)
                return (i - 1) / (float)samples;   // ひとつ前が山頂

            prev = val;
        }
        return 1f;  // 万一下降が無い場合
    }

    public static float FindMinTimeForValue(AnimationCurve curve, float targetValue, float timeStart = 0f, float timeEnd = 1f, float precision = 0.001f)
    {
        if (curve == null || curve.length == 0)
            return -1f; // 無効なカーブの場合は-1を返す

        // 探索範囲
        float minTime = timeStart;
        float maxTime = timeEnd;

        // 曲線が単調でない可能性があるため、Evaluateで直接比較
        float currentValue = curve.Evaluate(minTime);
        if (Mathf.Abs(currentValue - targetValue) < precision)
            return minTime; // 開始時点で一致する場合

        // 二分探索で最小の時間を探す
        while (maxTime - minTime > precision)
        {
            float midTime = (minTime + maxTime) / 2f;
            currentValue = curve.Evaluate(midTime);

            if (Mathf.Abs(currentValue - targetValue) < precision)
            {
                // 目標値に十分近い場合、さらに小さい時間がないか左側を探索
                maxTime = midTime;
            }
            else if (currentValue < targetValue)
            {
                minTime = midTime; // 値が小さい場合、右側を探索
            }
            else
            {
                maxTime = midTime; // 値が大きい場合、左側を探索
            }
        }

        // 最終的な時間を評価し、目標値に十分近いか確認
        float finalTime = (minTime + maxTime) / 2f;
        if (Mathf.Abs(curve.Evaluate(finalTime) - targetValue) < precision)
            return finalTime;

        return -1f; // 目標値に一致する時間が見つからない場合
    }

    // パブリックメソッド：ブレンドシェイプを遷移させる
    public void BlendshapeTransition(SkinnedMeshRenderer skm, int blendshapeIndex, float startWeight, float goalWeight, float duration)
    {
        // コルーチンを開始
        StartCoroutine(BlendshapeTransitionCoroutine(skm, blendshapeIndex, startWeight, goalWeight, duration));
    }

    // 内部コルーチン：実際のブレンドシェイプ変化処理
    private IEnumerator BlendshapeTransitionCoroutine(SkinnedMeshRenderer skm, int blendshapeIndex, float startWeight, float goalWeight, float duration)
    {
        float elapsed = 0f;

        // 初期値をセット（念のため）
        skm.SetBlendShapeWeight(blendshapeIndex, startWeight);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentWeight = Mathf.Lerp(startWeight, goalWeight, t);
            skm.SetBlendShapeWeight(blendshapeIndex, currentWeight);
            blendshapeCache[blendshapeIndex] = currentWeight;
            yield return null;
        }

        // 最終的なウェイトを正確に設定
        skm.SetBlendShapeWeight(blendshapeIndex, goalWeight);
            blendshapeCache[blendshapeIndex] = goalWeight;
    }

    void UpdateBlendshpes()
    {
        var blendshapeData = new Dictionary<int, (float startSum, float goalSum, int count)>();

        // --- 1. currentEmotionTags 側の goalWeight を集計 ---
        foreach (var tag in currentEmotionTags)
        {
            int proxyIndex = GetProxyIndex(tag.emotion);
            if (proxyIndex == -1) continue;

            var proxy = mapping.proxies[proxyIndex];
            float rate = Mathf.Lerp(0f, 100f, (tag.magnitude / 5f) / currentEmotionTags.Count);

            foreach (var bs in proxy.blendshapes)
            {
                if (!blendshapeData.ContainsKey(bs.index))
                    blendshapeData[bs.index] = (0f, 0f, 0);

                var entry = blendshapeData[bs.index];
                entry.goalSum += rate;
                entry.startSum += blendshapeCache[bs.index];
                entry.count += 1;
                blendshapeData[bs.index] = entry;
            }
        }

        // --- 2. lastEmotionTags 側の restPose を適用するものを検出 ---
        foreach (var tag in lastEmotionTags)
        {
            // current に存在するものは既に処理済みなのでスキップ
            if (currentEmotionTags.Any(t => t.emotion.Equals(tag.emotion, System.StringComparison.OrdinalIgnoreCase)))
                continue;

            int proxyIndex = GetProxyIndex(tag.emotion);
            if (proxyIndex == -1) continue;

            var proxy = mapping.proxies[proxyIndex];

            foreach (var bs in proxy.blendshapes)
            {
                float restWeight = restPoseMap.TryGetValue(bs.index, out var val) ? val : 0f;

                if (!blendshapeData.ContainsKey(bs.index))
                    blendshapeData[bs.index] = (0f, 0f, 0);

                var entry = blendshapeData[bs.index];
                entry.goalSum += restWeight;
                entry.startSum += blendshapeCache[bs.index];
                entry.count += 1;
                blendshapeData[bs.index] = entry;
            }
        }

        // --- 3. 集計済みのデータを使って平均し、補間開始 ---
        foreach (var kv in blendshapeData)
        {
            int index = kv.Key;
            float start = kv.Value.startSum / kv.Value.count;
            float goal = kv.Value.goalSum / kv.Value.count;

            BlendshapeTransition(skinnedMeshRenderer, index, start, goal, emotionTransitionDuration);
            //blendshapeCache[index] = goal;
        }

        // --- 4. タグの状態更新 ---
        lastEmotionTags.Clear();
        lastEmotionTags.AddRange(currentEmotionTags);
    }

    private void InitializeBlinkAnimation()
    {
        // キーフレーム間の傾きを計算
        float slope1 = (1.0f - 0f) / (0.35f - 0f);      // 0→0.35の傾き
        float slope2 = (0f - 1.0f) / (1.0f - 0.35f);    // 0.35→1.0の傾き

        // キーフレームを作成し、タンジェントを設定
        Keyframe kf1 = new Keyframe(0f, 0f);
        kf1.outTangent = slope1;                         // 次のキーフレームへの傾き

        Keyframe kf2 = new Keyframe(0.35f, 1.0f);
        kf2.inTangent = slope1;                          // 前のキーフレームからの傾き
        kf2.outTangent = slope2;                         // 次のキーフレームへの傾き

        Keyframe kf3 = new Keyframe(1.0f, 0f);
        kf3.inTangent = slope2;                          // 前のキーフレームからの傾き

        blinkAnimation = new AnimationCurve(kf1, kf2, kf3);
    }
    //private void InitializeBlinkAnimation()
    //{
    //    blinkAnimation = new AnimationCurve(
    //        new Keyframe(0f, 0f),      // 時間0で値0（目が開いた状態）
    //        new Keyframe(0.35f, 1.0f), // 時間0.35で値1（目が閉じた状態）
    //        new Keyframe(1.0f, 0f)     // 時間1で値0（目が再び開いた状態）
    //    );
    //}

    // コンポーネントがアタッチされたときやリセットされたときに呼ばれる
    private void Reset()
    {
        // blinkAnimation をデフォルト値で初期化
        InitializeBlinkAnimation();
    }

    private void EditorDebugMode()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            consciousnessLevel = BlendshapeFacialAnimator.Consciousness.Normal;
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            consciousnessLevel = BlendshapeFacialAnimator.Consciousness.Sleeping;
        }
        if (Input.GetKeyDown(KeyCode.U))
        {
            consciousnessLevel = BlendshapeFacialAnimator.Consciousness.Unconscious;
        }
    }
}