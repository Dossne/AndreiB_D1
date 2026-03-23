using System.Collections.Generic;
using UnityEngine;

public static class LeaderboardStorage
{
    private const string ScoreKeyPrefix = "leaderboard_score_";
    private const int MaxScores = 10;

    public static void RecordScore(int score)
    {
        if (score <= 0)
        {
            return;
        }

        var scores = GetTopScores();
        scores.Add(score);
        scores.Sort((left, right) => right.CompareTo(left));

        if (scores.Count > MaxScores)
        {
            scores.RemoveRange(MaxScores, scores.Count - MaxScores);
        }

        SaveScores(scores);
    }

    public static List<int> GetTopScores()
    {
        var scores = new List<int>(MaxScores);
        for (var i = 0; i < MaxScores; i++)
        {
            var key = $"{ScoreKeyPrefix}{i}";
            if (PlayerPrefs.HasKey(key))
            {
                scores.Add(PlayerPrefs.GetInt(key));
            }
        }

        return scores;
    }

    private static void SaveScores(List<int> scores)
    {
        for (var i = 0; i < MaxScores; i++)
        {
            var key = $"{ScoreKeyPrefix}{i}";
            if (i < scores.Count)
            {
                PlayerPrefs.SetInt(key, scores[i]);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        PlayerPrefs.Save();
    }
}
