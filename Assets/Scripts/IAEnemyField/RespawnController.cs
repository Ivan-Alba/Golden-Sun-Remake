using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnController : MonoBehaviour
{
    [SerializeField] GameObject[] enemyList;
    GameObject player;


    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
    }
    // Start is called before the first frame update
    void Start()
    {
        RespawnEnemy();
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.childCount == 0)
        {
            if (Vector3.Distance(player.transform.position, transform.position) > 50) {
                RespawnEnemy();
            }
        }
    }

     void RespawnEnemy()
    {
        int randomEnemy = Random.Range(0, enemyList.Length);
        GameObject newEnemy = Instantiate(enemyList[randomEnemy], gameObject.transform.position, Quaternion.identity);
        newEnemy.transform.SetParent(gameObject.transform);
        newEnemy.GetComponent<Ai>().enabled = true;
    }

    public GameObject GetRandomEnemy()
    {
        int random = Random.Range(0, enemyList.Length);
        GameObject randomEnemy = enemyList[random];
        return randomEnemy;
    }

    public GameObject GetEnemyLike(GameObject likeThis)

    {
        foreach (GameObject prefab in enemyList) {
            if (likeThis.name.StartsWith(prefab.name))
                return prefab;
        }
        return null;
    }

}
