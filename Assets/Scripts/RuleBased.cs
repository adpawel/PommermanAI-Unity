using UnityEngine;
using System.Collections.Generic;

public class RuleBased
{
    enum BotState
    {
        Explore,
        EscapeBomb,
        BombCrate,
        AttackEnemy
    }


    readonly PommermanAgent agent;
    BotState state = BotState.Explore;
    DecisionAgent ml;

    static readonly Vector3[] dirs =
    {
        Vector3.forward,
        Vector3.back,
        Vector3.left,
        Vector3.right
    };

    public RuleBased(PommermanAgent agent)
    {
        this.agent = agent;
        ml = agent.GetComponentInChildren<DecisionAgent>();
    }

    public void Tick()
    {
        if (agent == null || !agent.isAlive) return;

        Bomb dangerBomb = FindDangerousBomb();

        int intent = ml != null ? ml.chosenAction : 0;

        if (dangerBomb != null)
        {
            state = BotState.EscapeBomb;
            Escape(dangerBomb);
            return;
        }

        switch (intent)
        {
            case 1:
                PommermanAgent target = FindAttackTarget();
                if (target != null && HasEscapeAfterBomb())
                {
                    state = BotState.AttackEnemy;
                    Attack(target);
                    return;
                }
                break;
            case 2:
                if (HasBreakableAdjacent())
                {
                    state = BotState.BombCrate;
                    agent.PlaceBomb();
                    return;
                }
                break;
            default:
                state = BotState.Explore;
                Explore();
                break;
        }
    }

    void Explore()
    {
        List<Vector3> shuffled = new List<Vector3>(dirs);
        Shuffle(shuffled);

        foreach (Vector3 d in shuffled)
        {
            Vector3 target = agent.transform.position + d;
            if (agent.CanMoveTo(target))
            {
                agent.MoveTo(target);
                return;
            }
        }
    }

    void Escape(Bomb bomb)
    {
        foreach (Vector3 d in dirs)
        {
            Vector3 target = agent.transform.position + d;
            if (agent.CanMoveTo(target) && !IsInBombRange(target, bomb))
            {
                agent.MoveTo(target);
                return;
            }
        }

        foreach (Vector3 d1 in dirs)
        {
            Vector3 mid = agent.transform.position + d1;
            if (!agent.CanMoveTo(mid)) continue;

            foreach (Vector3 d2 in dirs)
            {
                Vector3 target = mid + d2;
                if (agent.CanMoveTo(target) && !IsInBombRange(target, bomb))
                {
                    agent.MoveTo(mid);
                    return;
                }
            }
        }
        // panic: no move
    }

    Bomb FindDangerousBomb()
    {
        Bomb best = null;
        float bestTime = float.PositiveInfinity;

        foreach (Bomb b in Object.FindObjectsOfType<Bomb>())
        {
            float t = b.TimeToExplode;

            if (t > 1.2f) continue;
            if (!IsInBombRange(agent.transform.position, b)) continue;

            if (t < bestTime)
            {
                bestTime = t;
                best = b;
            }
        }

        return best;
    }

    bool HasBreakableAdjacent()
    {
        foreach (Vector3 d in dirs)
        {
            Vector3 check = agent.transform.position + d;
            Collider[] hits = Physics.OverlapBox(check, Vector3.one * 0.45f);
            foreach (Collider c in hits)
                if (c != null && c.CompareTag("WallBreakable"))
                    return true;
        }
        return false;
    }

    bool IsInBombRange(Vector3 pos, Bomb bomb)
    {
        Vector2Int p = ToGrid(pos);
        Vector2Int b = ToGrid(bomb.transform.position);

        if (p.x == b.x)
            return Mathf.Abs(p.y - b.y) <= bomb.blastRadius && !IsBlocked(b, p);

        if (p.y == b.y)
            return Mathf.Abs(p.x - b.x) <= bomb.blastRadius && !IsBlocked(b, p);

        return false;
    }

    bool IsBlocked(Vector2Int from, Vector2Int to)
    {
        Vector2Int dir = new Vector2Int(
            Mathf.Clamp(to.x - from.x, -1, 1),
            Mathf.Clamp(to.y - from.y, -1, 1)
        );

        Vector2Int cur = from + dir;

        while (cur != to)
        {
            Vector3 world = new Vector3(cur.x, 0.5f, cur.y);
            Collider[] hits = Physics.OverlapBox(world, Vector3.one * 0.45f);

            foreach (Collider h in hits)
            {
                if (h == null) continue;
                if (h.CompareTag("WallSolid") || h.CompareTag("WallBreakable"))
                    return true;
            }

            cur += dir;
        }

        return false;
    }

    Vector2Int ToGrid(Vector3 pos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(pos.x),
            Mathf.RoundToInt(pos.z)
        );
    }

    void Shuffle(List<Vector3> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    PommermanAgent FindAttackTarget()
    {
        PommermanAgent[] agents = Object.FindObjectsOfType<PommermanAgent>();

        foreach (PommermanAgent other in agents)
        {
            if (other == agent) continue;
            if (!other.isAlive) continue;

            if (IsInAttackLine(other))
                return other;
        }
        return null;
    }

    bool IsInAttackLine(PommermanAgent target)
    {
        Vector2Int a = ToGrid(agent.transform.position);
        Vector2Int t = ToGrid(target.transform.position);

        // ten sam rząd
        if (a.x == t.x &&
            Mathf.Abs(a.y - t.y) <= 2 && //Blast radius
            !IsBlocked(a, t))
            return true;

        // ta sama kolumna
        if (a.y == t.y &&
            Mathf.Abs(a.x - t.x) <= 2 &&
            !IsBlocked(a, t))
            return true;

        return false;
    }

    bool HasEscapeAfterBomb()
    {
        foreach (Vector3 d in dirs)
        {
            Vector3 escape = agent.transform.position + d;
            if (agent.CanMoveTo(escape))
                return true;
        }
        return false;
    }

    void Attack(PommermanAgent target)
    {
        agent.MoveTo(target.transform.position);
        agent.PlaceBomb();
    }
}