# Deploying QuizBattle on Render.com (100% Free Forever)

Render provides free cloud hosting with automatic **HTTPS**, **WSS (secure WebSockets)**, and custom domains with no trial expiration.

---

## Step-by-Step Deployment Guide

### Step 1: Push Your Code to GitHub (or GitLab)

If your project is not already on GitHub:
1. Go to [GitHub.com](https://github.com/) and create a new repository (e.g. `quizbattle`).
2. In your terminal (in `D:\game`), run:
   ```cmd
   git init
   git add .
   git commit -m "Initial commit for QuizBattle"
   git branch -M main
   git remote add origin https://github.com/YOUR_USERNAME/quizbattle.git
   git push -u origin main
   ```

---

### Step 2: Create a Free Web Service on Render

1. Sign up / Log in to **[Render.com](https://dashboard.render.com/)** (you can sign in with your GitHub account).
2. Click the blue **"New +"** button at the top right -> Select **"Web Service"**.
3. Choose **"Build and deploy from a Git repository"** -> Click **Next**.
4. Select your **`quizbattle`** repository.
5. Fill in the deployment settings:
   - **Name**: `quizbattle` (or any name you like)
   - **Region**: Choose the closest region to you (e.g., *Ohio (US East)* or *Frankfurt (EU)*)
   - **Language / Runtime**: Select **Docker** (Render will automatically use your `Dockerfile`)
   - **Instance Type**: Select **Free** ($0.00/month)
6. Under **Advanced** -> **Environment Variables**, add:
   - `MODE` = `wan`
   - `SERVER_NAME` = `Classroom QuizBattle`
   - `JWT_SECRET` = *(Click "Generate" or type any secret password)*
7. Click **"Create Web Service"**.

---

### Step 3: Access Your Game!

Render will now build your container and deploy the app. Once finished (typically 2–3 minutes), Render gives you a public URL like:
```
https://quizbattle-xxxx.onrender.com
```

- **Teacher Management Portal**: Open `https://quizbattle-xxxx.onrender.com/` in your browser.
- **Students Play in Browser (WebGL)**: Open `https://quizbattle-xxxx.onrender.com/play` in any browser.
- **Mobile / Desktop Apps**: Enter `quizbattle-xxxx.onrender.com` with port `443` on the connect screen.

---

## Good to Know About Render's Free Tier
- **Automatic HTTPS / WSS**: Secure certificates are created and renewed automatically by Render.
- **Sleep after inactivity**: On the Free plan, if no one visits the site for 15 minutes, the server goes to sleep to save resources. When a student or teacher visits the URL, it automatically wakes up within ~30–50 seconds.
- **Continuous Deployment**: Every time you push new code to your GitHub repo, Render automatically rebuilds and updates your live game!
