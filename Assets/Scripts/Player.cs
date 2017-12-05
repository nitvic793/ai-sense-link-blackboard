using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour {

    public float Health = 100F;

	void Start () {
      
	}

    private void RestartCurrentScene()
    {
        int scene = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }

    void Update () {
        if (Health == 0)
        {
            RestartCurrentScene();
        }
    }

    public void InflictDamage(float hit)
    {
        Health -= hit;
        if (Health < 0) Health = 0;
    }
}
