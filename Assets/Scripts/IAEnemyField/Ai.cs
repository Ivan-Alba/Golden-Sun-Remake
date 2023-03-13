using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Ai : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private GameObject parentRespawnZone;
    private GameObject player;

    [Header("FollowSettings")]
    [SerializeField] float followRadius;
    [SerializeField] float followSpeed;
    [SerializeField] int maxFollowDistance;
    [SerializeField] LayerMask playerMask;


    //float moveByPositionRadius;

    private bool canMove = true;
    private bool followingPlayer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        player = GameObject.Find("Player");
    }

    private void Start()
    {
        parentRespawnZone = transform.parent.gameObject;
    }
    private void Update()
    {
        //Check if player is inside alert radius of enemy
        followingPlayer = Physics.CheckSphere(transform.position, followRadius, playerMask);

        //Following player
        if (followingPlayer && Vector3.Distance(player.transform.position, transform.parent.gameObject.transform.position) <= maxFollowDistance)
        {
            StopAllCoroutines();
            canMove = true;

            transform.LookAt(new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z));
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(player.transform.position.x, transform.position.y,
               player.transform.position.z), followSpeed * Time.deltaTime);
            animator.SetFloat("Move", followSpeed);
        }
        else
        {

            if (!agent.hasPath && canMove)
            {
                StartCoroutine(Roaming());
                canMove = false;
            }
            animator.SetFloat("Move", agent.velocity.magnitude);
        }


    }

    public void CollissionWithPlayer()

    {
        followingPlayer = false;
        followSpeed = 0;
        transform.GetComponent<Animator>().enabled = false;
        transform.GetComponent<NavMeshAgent>().enabled = false;
    }

    private IEnumerator Roaming()
    {
        agent.SetDestination(parentRespawnZone.GetComponent<GetPoint>().GetRandomPoint());
        float waitTime = Random.Range(5, 11);
        yield return new WaitForSeconds(waitTime);
        canMove = true;
    }

#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        //Radio enemigo movimiento por posicion
        //Gizmos.DrawWireSphere(transform.position, moveByPositionRadius);

        //Radio alerta enemigo
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, followRadius);
    }

#endif
}