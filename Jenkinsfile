pipeline {
   agent any

   stages {
        stage('Checkout') {
             steps {
                git branch: 'DEV', credentialsId: 'git', url: 'https://github.com/cartsp/fileconversion.git'
             }
             
        }
        stage('Nuget restore') {
            steps {
                sh label: '', script: 'dotnet restore'
            }
        }
        stage('Build') {
            steps {
                sh label: '', script: 'dotnet publish -c Release'
            }
        }
        stage('Test') {
            steps {
                sh label: '', script: 'dotnet test'
            }
        }
    }
}