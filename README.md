# FileWatcherSMB

Un proiect .NET complet care monitorizează în timp real fișierele dintr-un folder partajat SMB și transmite evenimentele detectate către o coadă RabbitMQ, utilizând un sistem asincron eficient.

## Introducere

FileWatcherSMB este o aplicație care automatizează procesul de urmărire a fișierelor dintr-un director SMB partajat de o mașină virtuală Ubuntu, montat pe un sistem Windows. Acest proiect urmărește să creeze un sistem de logare și procesare în timp real a evenimentelor de fișier (creare, modificare, redenumire), folosind tehnologii moderne precum:

- SMB pentru partajarea fișierelor între mașini

- FileSystemWatcher din .NET pentru monitorizarea locală

- RabbitMQ pentru mesagerie și decuplarea fluxului de procesare

- Docker pentru rularea rapidă și izolată a RabbitMQ

 ## Conectarea SMB și Monitorizarea Fișierelor

### Context tehnic

SMB (Server Message Block) este un protocol de rețea pentru partajarea fișierelor între diferite dispozitive. În acest proiect, mașina virtuală cu Ubuntu acționează ca server SMB, iar Windows-ul gazdă montează acel folder partajat ca unitate locală.

### Server SMB – configurare pe Ubuntu


1. Pe serverul Ubuntu (VM), am instalat și configurat samba:
```
sudo apt update
sudo apt install samba
```
Explicație:

* ```apt update``` actualizează lista de pachete.

* ```apt install samba``` instalează Samba — software-ul care permite partajarea fișierelor între Linux și Windows.


2. Crearea folderului care va fi partajat

```
mkdir -p /home/user_name/smbshare
```
Explicație:

* Creezi folderul pe care vrei să-l partajezi.

* ```-p``` creează și directoarele părinte dacă nu există (```home/user_name``` în acest caz).
```
sudo chown timi:timi /home/user_name/smbshare
```
* Asta asigură că utilizatorul ```user_name``` are drepturi de scriere în folderul partajat.


 3. Adăugarea configurării în fișierul ```smb.conf```

```
sudo nano /etc/samba/smb.conf
```
Adaugi la finalul fișierului:
```
[smbshare]
   path = /home/user_name/smbshare
   browseable = yes
   read only = no
   guest ok = no
   valid users = user_name
```
Explicație:

* ```[smbshare]``` – numele cu care va apărea folderul în rețea.

* ```path``` – locația reală a folderului.

* ```browseable``` = yes – permite să fie vizibil în rețea.

* ```read only``` = no – oferă permisiune de scriere.

* ```guest ok``` = no – nu permite acces anonim.

* ```valid users``` = user_name – doar utilizatorul ```user_name``` poate accesa.


 4. Crearea unui utilizator Samba

```
sudo smbpasswd -a user_name
```
Explicație:

* Adaugă utilizatorul ```user_name``` în sistemul Samba și setează o parolă.

* Trebuie să existe deja ca utilizator în Ubuntu.


 5. Repornirea serviciului Samba

```
sudo systemctl restart smbd
```
Explicație:

* Aplică noile modificări din fișierul ```smb.conf.```


### Montarea partajării pe Windows

Pe Windows, folderul a fost montat cu următorii pași:
1. **Deschide File Explorer** → click pe **This PC**

2. **Click dreapta** în fereastră → **Map network drive**

3. Am ales **Z:**

4. La **Folder**, am scris:

```
\\192.168.1.10\smbshare
```
(înlocuiește IP-ul cu adresa IP a Ubuntu-ului tău, și ```smbshare``` cu numele partajării din smb.conf)

5. Am bifat :

* _ _Reconnect at sign-in_ _ – ca să se reconecteze automat după restart


6. Click **Finish**

7. Am introdus userul ```user_name``` și parola setată cu ```smbpasswd -a user_name```

