import { useEffect, useRef, useState } from 'react';

// Navbar'daki küçük açılır menüler için: dışarı tıklayınca ve Escape ile kapanır,
// Escape ile kapanınca odak butona döner.
export default function useDropdown() {
  const [open, setOpen] = useState(false);
  const wrapperRef = useRef(null);
  const buttonRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;
    const handleClick = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target)) setOpen(false);
    };
    const handleKey = (e) => {
      if (e.key === 'Escape') {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener('mousedown', handleClick);
    document.addEventListener('keydown', handleKey);
    return () => {
      document.removeEventListener('mousedown', handleClick);
      document.removeEventListener('keydown', handleKey);
    };
  }, [open]);

  return { open, setOpen, wrapperRef, buttonRef, toggle: () => setOpen((o) => !o), close: () => setOpen(false) };
}
