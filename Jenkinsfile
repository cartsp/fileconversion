pipeline {
   agent any

   stages {
        stage('Checkout') {
             steps {
                git credentialsId: 'e8c84390-ef5b-4c17-bc9c-76c39f60f039', url: 'https://github.com/cartsp/fileconversion.git'
             }
             
        }
    }
}