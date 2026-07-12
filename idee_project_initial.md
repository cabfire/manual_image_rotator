# Projet : Manual Image Rotator pour NINA

## Objectif :
Créer un plugin NINA qui se comporte comme un rotateur manuel assisté. L’utilisateur définit un angle cible, déclenche un Rotate, puis tourne physiquement son rotateur/caméra. Le plugin capture régulièrement des images, mesure l’angle courant par rapport à une image de référence, affiche l’écart angulaire, et termine automatiquement le Rotate lorsque la tolérance est atteinte.

## Concept de plug-in équipement NINA :

* Nom proposé : Manual Image Rotator
* Type logique : rotateur manuel / virtual rotator
* `TargetPosition` : angle cible demandé par l’utilisateur
* `Position` : angle courant mesuré à partir des images
* `IsMoving = true` pendant la phase d’assistance
* `IsMoving = false` dès que `abs(delta) <= tolerance`
* `Halt` / `Cancel` doit interrompre la boucle avec relève du dernier angle connu pour la position courante de l'angle.
* `Expostion` : temps d'exposition pour la photo

## Workflow utilisateur :

1. L’utilisateur saisit une target, par exemple 45.0°.
2. L’utilisateur clique sur `Rotate to target` ou bien c'est demandé par le système : par exemple le sequencer
3. Le plugin capture une image de référence.
4. Le plugin détecter et mémoriser plusieurs étoiles de référence.
4. Le plugin propose à l'utilisateur de tourner dans un sens (clockWize ou Anti)
5. Le plugin rentre dans une boucle : capture une image courante.
6. Le plugin détecte les étoiles correspondantes.
7. Le plugin calcule l’angle courant.
8. Il affiche :

   * image de référence
   * image courante
   * étoiles sélectionnées / matchées
   * angle courant
   * angle cible
   * delta restant
   * sens de rotation
9. Tant que la target n’est pas atteinte, le plugin reste en `IsMoving`.
10. Quand l’écart est inférieur à la tolérance, le plugin sort automatiquement de la boucle et renvoie la position d'angle courante
11. si l'utilisateur click sur "Cancel" alors le plug-in renvoie l'angle courant

## Interface souhaitée :

* Thème sombre compatible NINA.
* Affichage de l’image de réference avec étoiles sélectionnées
* Affichage de l'image courante en superposition (50% opacité par default)
* Slider d'opacité
* Affichage graphique d’un angle dessiné entre deux directions :

  * direction référence
  * direction courante
* Valeur de l’angle affichée directement sur l’image.
* Couleurs différentes :

  * référence en vert
  * courant en orange/bleu
* Panneau latéral :

  * Target angle
  * Current angle
  * Delta
  * Direction : tourner horaire / anti-horaire
  * Tolérance
  * Status : Moving / Target reached
  * champ de temps d'exposition photo 
* Log en bas ou à droite.

## Gestion image / exposition :

* Prévoir un réglage du temps d’exposition pour l’image courante.
* Option `Lock settings to reference` pour garder exposition/gain/filtre identiques à l’image de référence.
* Option `Auto exposure` possible pour faciliter la détection d’étoiles.
* Réglages utiles :

  * Exposure
  * Gain
  * Filter
  * Refresh interval
  * Continuous update ON/OFF

## Algorithme recommandé :

* Ne pas utiliser une seule étoile : au minimum deux, idéalement 5 à 10 étoiles.
* Détecter les centroïdes des étoiles sur l’image de référence.
* Détecter les centroïdes sur l’image courante.
* Matcher les étoiles entre référence et courant.
* Estimer la transformation 2D :

  * translation
  * rotation
  * éventuellement scale très proche de 1
* Utiliser RANSAC ou rejet d’outliers pour robustesse.
* Calculer l’angle de rotation entre l’image de référence et l’image courante.
* Normaliser le delta dans l’intervalle [-180°, +180°].

## Pseudo-code :

Move(target):
TargetPosition = target
IsMoving = true

```
while IsMoving:
    image = CaptureCurrentImage()
    currentAngle = MeasureRotationAngle(referenceImage, image)
    Position = currentAngle

    delta = Normalize(TargetPosition - Position)

    UpdateDisplay(Position, TargetPosition, delta)

    if abs(delta) <= Tolerance:
        IsMoving = false
        Status = "Target reached"
        break

    wait(refreshInterval)
```

Halt():
IsMoving = false
Status = "Cancelled"

## Points importants :

* Le plugin ne motorise rien : il guide l’utilisateur pendant une rotation manuelle.
* Du point de vue NINA, il peut être vu comme un rotator dont le Move reste actif tant que l’utilisateur n’a pas tourné.
* Prévoir un indicateur de qualité :

  * nombre d’étoiles matchées
  * erreur RMS en pixels
  * confiance de la transformation
* Quand la qualité est mauvaise, afficher un warning plutôt qu’un angle faux.

## But final :
Permettre à l’utilisateur de retrouver ou atteindre un angle de caméra précis, sans rotateur motorisé, en visualisant directement la rotation sur l’image grâce aux étoiles matchées.