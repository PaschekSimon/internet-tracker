import './App.css'
import {useEffect, useState} from "react";

function App() {
  const [loggedIn, setLoggedIn] = useState<boolean>(false);

  function login() {
    // login with Spring backend and axios


    setLoggedIn(true);
  }

  useEffect(() => {
    login();
  }, [loggedIn]);

  return (
    <>
      {/* network stats*/}
      {/* history */}
      {/* configure  (power off, block user) */}


    </>
  )
}

export default App
